using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Friend.RemoveFriend;

/// <summary>
/// <c>DELETE /api/friends/{userId}</c>: removes a friend, or cancels a pending request in either
/// direction. The id is the other user's id. Requires a valid JWT.
/// Deletes whatever links the caller and that user. Returns 404 if there is nothing between them and 401 if the token has no valid user id.
/// </summary>
public static class RemoveFriendEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, behind the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/friends/{userId:guid}", HandleAsync)
            .WithName("RemoveFriend")
            .WithTags("Friends")
            .WithSummary("Remove a friend or cancel a request")
            .WithDescription("The id is the other user's id. Works on an accepted friendship and on a pending request in either direction.")
            .Responds<ApiResult>("Friendship or request removed.")
            .RespondsNotFound("There is no friendship or request with that user.")
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Remove a friend or cancel a request</summary>
    private static async Task<IResult> HandleAsync(
        Guid userId,
        FriendService friendService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var me))
            return Results.Unauthorized();

        var removed = await friendService.RemoveAsync(me, userId);
        return removed
            ? Results.Ok(new { success = true })
            : Results.NotFound(new { success = false, message = "No such friendship." });
    }
}
