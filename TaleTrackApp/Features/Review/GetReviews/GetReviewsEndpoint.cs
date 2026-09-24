using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Review.GetReviews;

public static class GetReviewsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/reviews", HandleAsync)
            .WithName("GetReviews")
            .WithTags("Reviews")
            .WithSummary("List the caller's reviews")
            .Responds<GetReviewsResponse>("The caller's reviews with a summary of each media.")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        ReviewService reviewService,
        ClaimsPrincipal user,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(GetReviewsEndpoint));

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var reviews = await reviewService.GetByUserIdAsync(userId);

            var data = reviews
                .Select(r => new ReviewWithMediaItem
                {
                    Id = r.Id,
                    MediaId = r.MediaId,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    Media = new ReviewMediaSummary
                    {
                        Id = r.Media?.Id,
                        TitleEN = r.Media?.TitleEN,
                        TitleES = r.Media?.TitleES,
                        Type = r.Media?.Type.ToString(),
                        PosterUrl = r.Media?.PosterUrl,
                        Author = r.Media?.Author,
                    }
                })
                .ToList();

            return Results.Ok(new GetReviewsResponse { Success = true, Count = data.Count, Data = data });
        }
        catch (Exception ex)
        {
            logger.LogError("Error retrieving reviews: {Message}", ex.Message);
            return Results.StatusCode(500);
        }
    }
}
