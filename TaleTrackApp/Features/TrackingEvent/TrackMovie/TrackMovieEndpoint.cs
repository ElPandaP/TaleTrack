using System.Security.Claims;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Security;
using TaleTrackApp.Services;

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
        BackgroundRunner background,
        ClaimsPrincipal user,
        ILogger<TrackMovieRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        try
        {
            var media = await mediaService.FindOrCreateAsync(request.Title, Model.MediaType.Movie, request.Minutes ?? 0,
                language: request.Language);
            await trackingEventService.UpsertAsync(userId, media.Id, request.Progress);

            logger.LogInformation("Movie tracking for user {UserId}, '{Title}'", userId, media.Title);

            // Fire-and-forget TMDB enrichment — the extension doesn't wait for this.
            if (MediaService.NeedsTmdbEnrichment(media, request.Language))
            {
                var (mediaId, title, language) = (media.Id, request.Title, request.Language);
                background.Run<MediaService>($"TMDB enrichment for media {mediaId}",
                    mediaService => mediaService.EnrichFromTmdbAsync(mediaId, title, Model.MediaType.Movie, language));
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
