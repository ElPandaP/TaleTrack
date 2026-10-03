using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.User;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.User.GetMe;

/// <summary>
/// <c>GET /api/users/me</c>: the caller's own profile, including email and feed-privacy settings.
/// Requires a valid JWT.
/// Loads the caller's profile. Returns 404 if the account no longer exists and 401 if the token has no valid user id.
/// </summary>
public static class GetMeEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, behind the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/users/me", HandleAsync)
            .WithName("GetMe")
            .WithTags("Users")
            .WithSummary("Get the caller's own profile")
            .Responds<GetMeResponse>("The profile, including email and feed-privacy settings.")
            .Responds(StatusCodes.Status404NotFound, "The account no longer exists.")
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Get the caller's own profile</summary>
    private static async Task<IResult> HandleAsync(
        UserService userService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var me = await userService.GetByIdAsync(userId);
        if (me == null) return Results.NotFound();

        return Results.Ok(new GetMeResponse
        {
            Success = true,
            Data = new MeData
            {
                Id = me.Id,
                Username = me.Username,
                Email = me.Email,
                AvatarUrl = me.AvatarUrl,
                CreatedAt = me.CreatedAt,
                Privacy = new FeedPrivacyData
                {
                    BookProgress = me.ShareBookProgress,
                    BookReviews = me.ShareBookReviews,
                    MovieProgress = me.ShareMovieProgress,
                    MovieReviews = me.ShareMovieReviews,
                    SeriesProgress = me.ShareSeriesProgress,
                    SeriesReviews = me.ShareSeriesReviews,
                },
            }
        });
    }
}
