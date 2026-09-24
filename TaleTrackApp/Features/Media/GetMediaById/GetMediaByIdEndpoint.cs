using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Features.TrackingEvent;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Media.GetMediaById;

public static class GetMediaByIdEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/media/{id:guid}", HandleAsync)
            .WithName("GetMediaById")
            .WithTags("Media")
            .WithSummary("Get a media's detail page")
            .WithDescription("Media data, the caller's own progress and review, and every user's reviews. Fields prefixed `my` are absent when the caller has not tracked or reviewed it.")
            .Responds<GetMediaByIdResponse>("The media detail.")
            .RespondsNotFound("The media does not exist.")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        MediaService mediaService,
        ReviewService reviewService,
        TrackingEventService trackingEventService,
        ClaimsPrincipal user,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(GetMediaByIdEndpoint));

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        try
        {
            var media = await mediaService.GetByIdAsync(id);
            if (media == null)
                return Results.NotFound(new { success = false, message = "Media not found" });

            var reviews = await reviewService.GetByMediaIdAsync(id);
            var myTracking = await trackingEventService.GetForMediaAsync(userId, id);
            var myProgress = SeriesProgressCalculator.ProgressFor(media, myTracking);

            var detail = new MediaDetail(media, reviews, reviews.FirstOrDefault(r => r.UserId == userId), myTracking, myProgress);
            return Results.Ok(GetMediaByIdResponse.From(detail, userId));
        }
        catch (Exception ex)
        {
            logger.LogError("Error retrieving media {Id}: {Message}", id, ex.Message);
            return Results.StatusCode(500);
        }
    }
}
