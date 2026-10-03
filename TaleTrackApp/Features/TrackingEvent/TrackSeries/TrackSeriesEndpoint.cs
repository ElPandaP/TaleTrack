using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Security;
using TaleTrackApp.Services;

namespace TaleTrackApp.Features.TrackingEvent.TrackSeries;

/// <summary>
/// <c>POST /api/tracking/series</c>: records progress on a series episode. Called by the Netflix
/// extension. Requires a valid JWT.
/// Finds or creates the series, stores the caller's progress on it and, if the media still lacks metadata, queues a background TMDB lookup. Returns 401 if the token has no valid user id.
/// </summary>
public static class TrackSeriesEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, with request validation and the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/tracking/series", HandleAsync)
            .WithName("TrackSeries")
            .WithTags("Tracking")
            .WithSummary("Record progress on a series episode")
            .WithDescription("Creates the series on first use. Only the latest season and episode reported is kept, so watching an earlier episode moves it back there. Metadata is filled in later from TMDB, in the background.")
            .Responds<ApiResult>("Progress recorded.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Record progress on a series episode</summary>
    private static async Task<IResult> HandleAsync(
        TrackSeriesRequest request,
        MediaService mediaService,
        TrackingEventService trackingEventService,
        BackgroundRunner background,
        ClaimsPrincipal user,
        ILogger<TrackSeriesRequest> logger)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        // A series has no single length (its episodes vary), so it is stored as 0; its
        // progress comes from SeasonEpisodeCounts instead.
        var media = await mediaService.FindOrCreateAsync(request.Title, Model.MediaType.Series, length: 0,
            language: request.Language);
        await trackingEventService.UpsertAsync(
            userId, media.Id, request.Progress,
            request.Season, request.Episode);

        logger.LogInformation("Series tracking for user {UserId}, '{Title}' S{Season}E{Episode}",
            userId, media.TitleEN ?? media.TitleES, request.Season, request.Episode);

        // Runs after the response is sent, so the extension never waits on TMDB.
        if (MediaService.NeedsTmdbEnrichment(media, request.Language))
        {
            var (mediaId, title, language) = (media.Id, request.Title, request.Language);
            background.Run<MediaService>($"TMDB enrichment for media {mediaId}",
                mediaService => mediaService.EnrichFromTmdbAsync(mediaId, title, Model.MediaType.Series, language));
        }

        return Results.Ok(new { success = true, message = "Tracking event added successfully" });
    }
}
