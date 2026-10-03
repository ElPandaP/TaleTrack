using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.User;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Features.Library;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.User.GetUserProfile;

/// <summary>
/// <c>GET /api/users/{id}</c>: another user's public profile. Requires a valid JWT.
/// Returns the user's public data, their per-type media counts and how they relate to the caller. Returns 404 if the user does not exist and 401 if the token has no valid user id.
/// </summary>
public static class GetUserProfileEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, behind the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/users/{id:guid}", HandleAsync)
            .WithName("GetUserProfile")
            .WithTags("Users")
            .WithSummary("Get a user's public profile")
            .WithDescription("Avatar, how many media of each type they track, and how they relate to the caller.")
            .Responds<GetUserProfileResponse>("The public profile.")
            .RespondsNotFound("The user does not exist.")
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Get a user's public profile</summary>
    private static async Task<IResult> HandleAsync(
        Guid id,
        UserService userService,
        FriendService friendService,
        LibraryService libraryService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var me))
            return Results.Unauthorized();

        var target = await userService.GetByIdAsync(id);
        if (target == null) return Results.NotFound(new { success = false });

        var relationship = await friendService.RelationshipAsync(me, id);
        var (book, movie, series, total) = await libraryService.CountByTypeAsync(id);

        return Results.Ok(new GetUserProfileResponse
        {
            Success = true,
            Data = new UserProfileData
            {
                Id = target.Id,
                Username = target.Username,
                AvatarUrl = target.AvatarUrl,
                CreatedAt = target.CreatedAt,
                Relationship = relationship,
                Counts = new MediaCounts { Book = book, Movie = movie, Series = series, Total = total },
            }
        });
    }
}
