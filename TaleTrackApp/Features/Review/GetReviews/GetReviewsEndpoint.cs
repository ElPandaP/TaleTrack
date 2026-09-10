using System.Security.Claims;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.Review.GetReviews;

public static class GetReviewsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/reviews", HandleAsync)
            .WithName("GetReviews")
            .WithDescription("Reviews written by the authenticated user, with their associated media")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        ReviewService reviewService,
        ClaimsPrincipal user,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(GetReviewsEndpoint));

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var reviews = await reviewService.GetByUserIdAsync(userId);

            var data = reviews
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    id = r.Id,
                    mediaId = r.MediaId,
                    rating = r.Rating,
                    comment = r.Comment,
                    createdAt = r.CreatedAt,
                    updatedAt = r.UpdatedAt,
                    media = new
                    {
                        id = r.Media?.Id,
                        title = r.Media?.Title,
                        type = r.Media?.Type,
                        posterUrl = r.Media?.PosterUrl,
                        author = r.Media?.Author,
                    }
                })
                .ToList();

            return Results.Ok(new { success = true, count = data.Count, data });
        }
        catch (Exception ex)
        {
            logger.LogError("Error retrieving reviews: {Message}", ex.Message);
            return Results.StatusCode(500);
        }
    }
}
