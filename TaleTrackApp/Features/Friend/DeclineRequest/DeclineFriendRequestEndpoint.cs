using System.Security.Claims;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Friend.DeclineRequest;

public static class DeclineFriendRequestEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/friends/requests/{id:guid}", HandleAsync)
            .WithName("DeclineFriendRequest")
            .WithDescription("Decline an incoming friend request")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        FriendService friendService,
        ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        var result = await friendService.DeclineAsync(userId, id);
        return result switch
        {
            RespondResult.Ok => Results.Ok(new { success = true }),
            RespondResult.NotFound => Results.NotFound(new { success = false, message = "Friend request not found." }),
            RespondResult.Forbidden => Results.Forbid(),
            _ => Results.StatusCode(500),
        };
    }
}
