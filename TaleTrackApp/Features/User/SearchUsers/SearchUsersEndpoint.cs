using TaleTrackApp.OpenApi;
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
            .WithTags("Users")
            .WithSummary("Find a user by exact username")
            .WithDescription("Used to send friend requests. A leading @ is ignored. When nobody has that username the call still succeeds and `user` is absent.")
            .Responds<SearchUsersResponse>("The match and its relationship to the caller, or an empty result.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
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
