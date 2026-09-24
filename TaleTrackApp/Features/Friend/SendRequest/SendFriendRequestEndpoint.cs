using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Friend.SendRequest;

public static class SendFriendRequestEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/friends/requests", HandleAsync)
            .WithName("SendFriendRequest")
            .WithTags("Friends")
            .WithSummary("Send a friend request")
            .WithDescription("Find the target's id with `GET /api/users/search`.")
            .Responds<ApiResult>("Request sent (code `sent`).")
            .RespondsBadRequest("Code `self`, `already_friends`, `already_pending` (you already asked) or `reverse_pending` (they already asked you).")
            .RespondsNotFound("The target user does not exist (code `target_not_found`).")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        SendFriendRequestRequest request,
        FriendService friendService,
        ClaimsPrincipal user,
        ILogger<SendFriendRequestRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        var (result, _) = await friendService.SendRequestAsync(userId, request.UserId!.Value);

        // `code` is a stable machine key the frontend maps to a localized message.
        return result switch
        {
            SendRequestResult.Ok => Results.Ok(new { success = true, code = "sent", message = "Friend request sent." }),
            SendRequestResult.TargetNotFound => Results.NotFound(new { success = false, code = "target_not_found", message = "User not found." }),
            SendRequestResult.Self => Results.BadRequest(new { success = false, code = "self", message = "You can't add yourself." }),
            SendRequestResult.AlreadyFriends => Results.BadRequest(new { success = false, code = "already_friends", message = "You are already friends." }),
            SendRequestResult.AlreadyPending => Results.BadRequest(new { success = false, code = "already_pending", message = "You already sent this user a request." }),
            SendRequestResult.ReversePending => Results.BadRequest(new { success = false, code = "reverse_pending", message = "This user already sent you a request." }),
            _ => Results.StatusCode(500),
        };
    }
}
