using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.Friend.SendRequest;

public class SendFriendRequestRequest
{
    [Required(ErrorMessage = "userId is required")]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }
}

public static class SendFriendRequestEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/friends/requests", HandleAsync)
            .WithName("SendFriendRequest")
            .WithDescription("Send a friend request to a user by id")
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
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Results.Unauthorized();

        var (result, _) = await friendService.SendRequestAsync(userId, request.UserId);

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
