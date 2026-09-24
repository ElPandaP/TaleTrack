using TaleTrackApp.OpenApi;
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
            .WithTags("Tracking")
            .WithSummary("Record progress on a film")
            .WithDescription("Creates the media on first use, matching by title, and keeps one tracking record per user and film. Progress never goes down through this endpoint; use `PUT /api/tracking/{mediaId}` to lower it. Posters and descriptions are filled in later from TMDB, in the background.")
            .Responds<ApiResult>("Progress recorded.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
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

            logger.LogInformation("Movie tracking for user {UserId}, '{Title}'", userId, media.TitleEN ?? media.TitleES);

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
