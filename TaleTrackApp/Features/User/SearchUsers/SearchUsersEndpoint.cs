using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TaleTrackApp.Features.User;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.User.SearchUsers;

public class SearchUsersRequest
{
    [Required(ErrorMessage = "username is required")]
    [StringLength(50, MinimumLength = 1)]
    public required string Username { get; set; }
}

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
            return Results.Ok(new { success = true, user = (object?)null });

        var relationship = await friendService.RelationshipAsync(userId, found.Id);

        return Results.Ok(new
        {
            success = true,
            user = new { userId = found.Id, username = found.Username, avatarUrl = found.AvatarUrl },
            relationship,
        });
    }
}
