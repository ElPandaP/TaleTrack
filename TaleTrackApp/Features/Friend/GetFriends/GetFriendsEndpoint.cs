using System.Security.Claims;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Friend.GetFriends;

public static class GetFriendsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/friends", HandleAsync)
            .WithName("GetFriends")
            .WithDescription("The user's accepted friends plus incoming and outgoing requests")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        FriendService friendService,
        ClaimsPrincipal user,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(GetFriendsEndpoint));
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        try
        {
            var friends = await friendService.GetFriendsAsync(userId);
            var incoming = await friendService.GetIncomingAsync(userId);
            var outgoing = await friendService.GetOutgoingAsync(userId);
            return Results.Ok(new GetFriendsResponse { Success = true, Friends = friends, Incoming = incoming, Outgoing = outgoing });
        }
        catch (Exception ex)
        {
            logger.LogError("Error retrieving friends: {Message}", ex.Message);
            return Results.StatusCode(500);
        }
    }
}
