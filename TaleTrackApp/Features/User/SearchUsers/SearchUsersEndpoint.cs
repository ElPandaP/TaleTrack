using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.User;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.User.SearchUsers;

/// <summary>
/// <c>GET /api/users/search</c>: finds a user by exact username, to send them a friend request.
/// Requires a valid JWT.
/// Looks the username up (case-insensitive, leading @ ignored). Finding nobody is not an error: the response simply has no user. Returns 401 if the token has no valid user id.
/// </summary>
public static class SearchUsersEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, with request validation and the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/users/search", HandleAsync)
            .WithName("SearchUsers")
            .WithTags("Users")
            .WithSummary("Find a user by exact username")
            .WithDescription("Used to send friend requests. A leading @ is ignored. When nobody has that username the call still succeeds and `user` is absent.")
            .Responds<SearchUsersResponse>("The match and its relationship to the caller, or an empty result.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Find a user by exact username</summary>
    private static async Task<IResult> HandleAsync(
        [AsParameters] SearchUsersRequest request,
        UserService userService,
        FriendService friendService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var found = await userService.GetByUsernameAsync(request.Username.TrimStart('@'));
        if (found == null)
            return Results.Ok(new SearchUsersResponse { Success = true });

        var relationship = await friendService.RelationshipAsync(userId, found.Id);

        return Results.Ok(new SearchUsersResponse
        {
            Success = true,
            User = new FoundUser { UserId = found.Id, Username = found.Username, AvatarUrl = found.AvatarUrl },
            Relationship = relationship,
        });
    }
}
