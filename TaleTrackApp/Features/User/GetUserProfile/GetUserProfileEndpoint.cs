using System.Security.Claims;
using TaleTrackApp.Features.User;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Features.Library;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.User.GetUserProfile;

public static class GetUserProfileEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/users/{id:guid}", HandleAsync)
            .WithName("GetUserProfile")
            .WithDescription("A user's public profile (avatar, counts, relationship to the caller)")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        UserService userService,
        FriendService friendService,
        LibraryService libraryService,
        ClaimsPrincipal user,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(GetUserProfileEndpoint));
        var meClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(meClaim) || !Guid.TryParse(meClaim, out Guid me))
            return Results.Unauthorized();

        try
        {
            var target = await userService.GetByIdAsync(id);
            if (target == null) return Results.NotFound(new { success = false });

            var relationship = await friendService.RelationshipAsync(me, id);
            var (book, movie, series, total) = await libraryService.CountByTypeAsync(id);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    id = target.Id,
                    username = target.Username,
                    avatarUrl = target.AvatarUrl,
                    createdAt = target.CreatedAt,
                    relationship,
                    counts = new { book, movie, series, total },
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError("Error retrieving user profile {Id}: {Message}", id, ex.Message);
            return Results.StatusCode(500);
        }
    }
}
