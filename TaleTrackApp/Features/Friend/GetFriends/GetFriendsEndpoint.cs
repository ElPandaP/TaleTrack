using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Friend.GetFriends;

/// <summary>
/// <c>GET /api/friends</c>: the caller's friends plus their incoming and outgoing pending requests.
/// Requires a valid JWT.
/// Loads the three lists. Returns 401 if the token has no valid user id.
/// </summary>
public static class GetFriendsEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, behind the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/friends", HandleAsync)
            .WithName("GetFriends")
            .WithTags("Friends")
            .WithSummary("List friends and pending requests")
            .Responds<GetFriendsResponse>("Accepted friends plus incoming and outgoing requests.")
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>List friends and pending requests</summary>
    private static async Task<IResult> HandleAsync(
        FriendService friendService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var friends = await friendService.GetFriendsAsync(userId);
        var incoming = await friendService.GetIncomingAsync(userId);
        var outgoing = await friendService.GetOutgoingAsync(userId);
        return Results.Ok(new GetFriendsResponse { Success = true, Friends = friends, Incoming = incoming, Outgoing = outgoing });
    }
}
