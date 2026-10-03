using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Friend.SendRequest;

/// <summary>
/// <c>POST /api/friends/requests</c>: sends a friend request to another user. Requires a valid JWT.
/// Creates the pending request. Every outcome carries a stable <c>code</c>: 200 with <c>sent</c>, 404 with <c>target_not_found</c>, or 400 with <c>self</c>, <c>already_friends</c>, <c>already_pending</c> or <c>reverse_pending</c>. Returns 401 if the token has no valid user id.
/// </summary>
public static class SendFriendRequestEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, with request validation and the user policy.</summary>
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

    /// <summary>Send a friend request</summary>
    private static async Task<IResult> HandleAsync(
        SendFriendRequestRequest request,
        FriendService friendService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var result = await friendService.SendRequestAsync(userId, request.UserId!.Value);

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
