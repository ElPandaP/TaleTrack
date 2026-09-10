using System.Security.Claims;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Auth;

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
        IServiceScopeFactory scopeFactory,
        ClaimsPrincipal user,
        ILogger<TrackSeriesRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Results.Unauthorized();

        try
        {
            var media = await mediaService.FindOrCreateAsync(request.Title, "Series", request.Minutes ?? 0);
            await trackingEventService.UpsertAsync(
                userId, media.Id, request.Progress,
                request.Season, request.Episode, request.EpisodeTitle);

            logger.LogInformation("Series tracking for user {UserId}, '{Title}' S{Season}E{Episode}",
                userId, media.Title, request.Season, request.Episode);

            // Fire-and-forget TMDB enrichment — the extension doesn't wait for this.
            if (!string.IsNullOrWhiteSpace(request.Language) &&
                (string.IsNullOrWhiteSpace(media.PosterUrl) || string.IsNullOrWhiteSpace(media.AltTitle)))
            {
                var (mediaId, title, language) = (media.Id, request.Title, request.Language);
                _ = Task.Run(async () =>
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var bgMedia = scope.ServiceProvider.GetRequiredService<MediaService>();
                    var bgTmdb = scope.ServiceProvider.GetRequiredService<TmdbService>();
                    var bgLog = scope.ServiceProvider.GetRequiredService<ILogger<TrackSeriesRequest>>();
                    try
                    {
                        var result = await bgTmdb.EnrichAsync(title, "Series", language);
                        if (result != null)
                            await bgMedia.ApplyTmdbEnrichmentAsync(mediaId, result);
                    }
                    catch (Exception ex)
                    {
                        bgLog.LogError(ex, "Background TMDB enrichment failed for mediaId={Id}", mediaId);
                    }
                });
            }

            return Results.Ok(new { success = true, message = "Tracking event agregado exitosamente" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating series tracking event");
            return Results.StatusCode(500);
        }
    }
}
