using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Friend.AcceptRequest;

/// <summary>
/// <c>PUT /api/friends/requests/{id}</c>: accepts an incoming friend request. Requires a valid JWT.
/// Accepts the request if it is pending and addressed to the caller. Returns 404 if there is no such pending request, 403 if it was sent to someone else and 401 if the token has no valid user id.
/// </summary>
public static class AcceptFriendRequestEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, behind the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/friends/requests/{id:guid}", HandleAsync)
            .WithName("AcceptFriendRequest")
            .WithTags("Friends")
            .WithSummary("Accept an incoming friend request")
            .WithDescription("The id is the `requestId` from the incoming list of `GET /api/friends`.")
            .Responds<ApiResult>("Now friends.")
            .Responds(StatusCodes.Status403Forbidden, "The request was not addressed to the caller.")
            .RespondsNotFound("The request does not exist.")
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Accept an incoming friend request</summary>
    private static async Task<IResult> HandleAsync(
        Guid id,
        FriendService friendService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var result = await friendService.AcceptAsync(userId, id);
        return result switch
        {
            RespondResult.Ok => Results.Ok(new { success = true }),
            RespondResult.NotFound => Results.NotFound(new { success = false, message = "Friend request not found." }),
            RespondResult.Forbidden => Results.Forbid(),
            _ => Results.StatusCode(500),
        };
    }
}
