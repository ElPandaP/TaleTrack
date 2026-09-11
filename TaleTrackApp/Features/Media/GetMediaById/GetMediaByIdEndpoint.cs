using System.Security.Claims;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Features.TrackingEvent;
using TaleTrackApp.Auth;
using TrackingEventModel = TaleTrackApp.Model.TrackingEvent;

namespace TaleTrackApp.Features.Media.GetMediaById;

public static class GetMediaByIdEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/media/{id:int}", HandleAsync)
            .WithName("GetMediaById")
            .WithDescription("A media's detail page: data, the user's progress and review, and every review")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        int id,
        MediaService mediaService,
        ReviewService reviewService,
        TrackingEventService trackingEventService,
        ClaimsPrincipal user,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(GetMediaByIdEndpoint));

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Results.Unauthorized();

        try
        {
            var media = await mediaService.GetByIdAsync(id);
            if (media == null)
                return Results.NotFound(new { success = false, message = "Media not found" });

            var reviews = await reviewService.GetByMediaIdAsync(id);
            var isSeries = media.Type == "Series";
            TrackingEventModel? myTracking = isSeries
                ? await trackingEventService.GetFurthestEpisodeAsync(userId, id)
                : await trackingEventService.GetLatestForMediaAsync(userId, id);
            var myProgress = isSeries
                ? SeriesProgress.Calculate(media.SeasonEpisodeCounts, myTracking?.Season, myTracking?.Episode) ?? myTracking?.Progress
                : myTracking?.Progress;
            var myReview = reviews.FirstOrDefault(r => r.UserId == userId);

            var response = new
            {
                success = true,
                data = new
                {
                    id = media.Id,
                    title = media.Title,
                    type = media.Type,
                    author = media.Author,
                    posterUrl = media.PosterUrl,
                    length = media.Length,
                    isbn = media.Isbn,
                    description = media.Description,
                    avgRating = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : (double?)null,
                    reviewCount = reviews.Count,
                    myProgress,
                    myLastEventDate = myTracking?.EventDate,
                    mySeason = isSeries ? myTracking?.Season : null,
                    myEpisode = isSeries ? myTracking?.Episode : null,
                    seasonEpisodeCounts = isSeries ? media.SeasonEpisodeCounts : null,
                    myReviewId = myReview?.Id,
                    myRating = myReview?.Rating,
                    myComment = myReview?.Comment,
                    reviews = reviews
                        .OrderByDescending(r => r.CreatedAt)
                        .Select(r => new
                        {
                            id = r.Id,
                            rating = r.Rating,
                            comment = r.Comment,
                            createdAt = r.CreatedAt,
                            username = r.User?.Username,
                            mine = r.UserId == userId,
                        }),
                }
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError("Error retrieving media {Id}: {Message}", id, ex.Message);
            return Results.StatusCode(500);
        }
    }
}
