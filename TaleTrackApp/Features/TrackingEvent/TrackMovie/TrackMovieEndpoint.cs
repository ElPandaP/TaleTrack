using System.Security.Claims;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.TrackingEvent.TrackMovie;

public static class TrackMovieEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/tracking/movies", HandleAsync)
            .WithName("TrackMovie")
            .WithDescription("Records watch progress for a film")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        TrackMovieRequest request,
        MediaService mediaService,
        TrackingEventService trackingEventService,
        IServiceScopeFactory scopeFactory,
        ClaimsPrincipal user,
        ILogger<TrackMovieRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Results.Unauthorized();

        try
        {
            var media = await mediaService.FindOrCreateAsync(request.Title, "Movie", request.Minutes ?? 0);
            await trackingEventService.UpsertAsync(userId, media.Id, request.Progress);

            logger.LogInformation("Movie tracking for user {UserId}, '{Title}'", userId, media.Title);

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
                    var bgLog = scope.ServiceProvider.GetRequiredService<ILogger<TrackMovieRequest>>();
                    try
                    {
                        var result = await bgTmdb.EnrichAsync(title, "Movie", language);
                        if (result != null)
                            await bgMedia.ApplyTmdbEnrichmentAsync(mediaId, result);
                    }
                    catch (Exception ex)
                    {
                        bgLog.LogError(ex, "Background TMDB enrichment failed for mediaId={Id}", mediaId);
                    }
                });
            }

            return Results.Ok(new { success = true, message = "Tracking event added successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating movie tracking event");
            return Results.StatusCode(500);
        }
    }
}
