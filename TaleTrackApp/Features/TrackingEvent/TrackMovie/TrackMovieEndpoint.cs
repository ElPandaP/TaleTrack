using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Security;
using TaleTrackApp.Services;

namespace TaleTrackApp.Features.TrackingEvent.TrackMovie;

/// <summary>
/// <c>POST /api/tracking/movies</c>: records progress on a film. Called by the Netflix extension.
/// Requires a valid JWT.
/// Finds or creates the film, stores the caller's progress on it and, if the media still lacks metadata, queues a background TMDB lookup. Returns 401 if the token has no valid user id.
/// </summary>
public static class TrackMovieEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, with request validation and the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/tracking/movies", HandleAsync)
            .WithName("TrackMovie")
            .WithTags("Tracking")
            .WithSummary("Record progress on a film")
            .WithDescription("Creates the media on first use, matching by title, and keeps one tracking record per user and film. Each report overwrites the stored progress, so it can go down as well as up. Posters and descriptions are filled in later from TMDB, in the background.")
            .Responds<ApiResult>("Progress recorded.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Record progress on a film</summary>
    private static async Task<IResult> HandleAsync(
        TrackMovieRequest request,
        MediaService mediaService,
        TrackingEventService trackingEventService,
        BackgroundRunner background,
        ClaimsPrincipal user,
        ILogger<TrackMovieRequest> logger)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var media = await mediaService.FindOrCreateAsync(request.Title, Model.MediaType.Movie, request.Minutes ?? 0,
            language: request.Language);
        await trackingEventService.UpsertAsync(userId, media.Id, request.Progress);

        logger.LogInformation("Movie tracking for user {UserId}, '{Title}'", userId, media.TitleEN ?? media.TitleES);

        // Runs after the response is sent, so the extension never waits on TMDB.
        if (MediaService.NeedsTmdbEnrichment(media, request.Language))
        {
            var (mediaId, title, language) = (media.Id, request.Title, request.Language);
            background.Run<MediaService>($"TMDB enrichment for media {mediaId}",
                mediaService => mediaService.EnrichFromTmdbAsync(mediaId, title, Model.MediaType.Movie, language));
        }

        return Results.Ok(new { success = true, message = "Tracking event added successfully" });
    }
}
