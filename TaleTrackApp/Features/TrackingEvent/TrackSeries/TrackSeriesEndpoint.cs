using System.Security.Claims;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Security;
using TaleTrackApp.Services;

namespace TaleTrackApp.Features.TrackingEvent.TrackSeries;

public static class TrackSeriesEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/tracking/series", HandleAsync)
            .WithName("TrackSeries")
            .WithDescription("Records watch progress for a series episode")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        TrackSeriesRequest request,
        MediaService mediaService,
        TrackingEventService trackingEventService,
        BackgroundRunner background,
        ClaimsPrincipal user,
        ILogger<TrackSeriesRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        try
        {
            // Length is a movie/book-only concept (a single runtime/page-count); a series'
            // episodes vary in length, so there's no meaningful single "length" to record.
            var media = await mediaService.FindOrCreateAsync(request.Title, "Series", length: 0,
                language: request.Language);
            await trackingEventService.UpsertAsync(
                userId, media.Id, request.Progress,
                request.Season, request.Episode);

            logger.LogInformation("Series tracking for user {UserId}, '{Title}' S{Season}E{Episode}",
                userId, media.Title, request.Season, request.Episode);

            // Fire-and-forget TMDB enrichment — the extension doesn't wait for this.
            if (!string.IsNullOrWhiteSpace(request.Language) &&
                (string.IsNullOrWhiteSpace(media.PosterUrl) ||
                 string.IsNullOrWhiteSpace(media.TitleEN) || string.IsNullOrWhiteSpace(media.TitleES)))
            {
                var (mediaId, title, language) = (media.Id, request.Title, request.Language);
                background.Run<MediaService>($"TMDB enrichment for media {mediaId}",
                    mediaService => mediaService.EnrichFromTmdbAsync(mediaId, title, "Series", language));
            }

            return Results.Ok(new { success = true, message = "Tracking event added successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating series tracking event");
            return Results.StatusCode(500);
        }
    }
}
