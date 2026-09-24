using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Friend.RemoveFriend;

public static class RemoveFriendEndpoint
{
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

    private static async Task<IResult> HandleAsync(
        Guid userId,
        FriendService friendService,
        ClaimsPrincipal user,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(RemoveFriendEndpoint));
        var meClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(meClaim) || !Guid.TryParse(meClaim, out Guid me))
            return Results.Unauthorized();

        var removed = await friendService.RemoveAsync(me, userId);
        return removed
            ? Results.Ok(new { success = true })
            : Results.NotFound(new { success = false, message = "No such friendship." });
    }
}
