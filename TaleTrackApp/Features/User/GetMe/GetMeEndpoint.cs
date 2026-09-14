using System.Security.Claims;
using TaleTrackApp.Features.User;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.User.GetMe;

public static class GetMeEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/user/me", HandleAsync)
            .WithName("GetMe")
            .WithDescription("The authenticated user's full profile (avatar, privacy, etc.)")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        UserService userService,
        ClaimsPrincipal user,
        ILoggerFactory loggerFactory)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        var me = await userService.GetByIdAsync(userId);
        if (me == null) return Results.NotFound();

        return Results.Ok(new
        {
            success = true,
            data = new
            {
                id = me.Id,
                username = me.Username,
                email = me.Email,
                avatarUrl = me.AvatarUrl,
                createdAt = me.CreatedAt,
                privacy = new
                {
                    bookProgress = me.ShareBookProgress,
                    bookReviews = me.ShareBookReviews,
                    movieProgress = me.ShareMovieProgress,
                    movieReviews = me.ShareMovieReviews,
                    seriesProgress = me.ShareSeriesProgress,
                    seriesReviews = me.ShareSeriesReviews,
                },
            }
        });
    }
}
