using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.User;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.User.GetMe;

public static class GetMeEndpoint
{
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
