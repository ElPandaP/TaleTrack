using System.Security.Claims;
using TaleTrackApp.Features.User;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.User.SearchUsers;

public static class SearchUsersEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/users/search", HandleAsync)
            .WithName("SearchUsers")
            .WithDescription("Find a user by exact username (for friend requests)")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] SearchUsersRequest request,
        UserService userService,
        FriendService friendService,
        ClaimsPrincipal user,
        ILogger<SearchUsersRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
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
