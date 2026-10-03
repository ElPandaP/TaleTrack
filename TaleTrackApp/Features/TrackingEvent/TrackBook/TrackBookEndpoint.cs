using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Security;
using TaleTrackApp.Services;

namespace TaleTrackApp.Features.TrackingEvent.TrackBook;

/// <summary>
/// <c>POST /api/tracking/books</c>: records reading progress on a book. Called by the KOReader
/// plugin. Requires a valid JWT.
/// Finds or creates the book, stores the caller's progress on it and, if the media still lacks metadata, queues a background Open Library lookup. Returns 401 if the token has no valid user id.
/// </summary>
public static class TrackBookEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, with request validation and the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/tracking/books", HandleAsync)
            .WithName("TrackBook")
            .WithTags("Tracking")
            .WithSummary("Record reading progress on a book")
            .WithDescription("Used by the KOReader plugin. The book is matched by ISBN, then by title and author, then by title alone, and created if unknown. Each report overwrites the stored progress, so it can go down as well as up.")
            .Responds<ApiResult>("Progress recorded.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Record reading progress on a book</summary>
    private static async Task<IResult> HandleAsync(
        TrackBookRequest request,
        MediaService mediaService,
        TrackingEventService trackingEventService,
        BackgroundRunner background,
        ClaimsPrincipal user,
        ILogger<TrackBookRequest> logger)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var media = await mediaService.FindOrCreateAsync(
            request.Title, Model.MediaType.Book, request.Pages ?? 0, request.Author, request.Isbn);

        await trackingEventService.UpsertAsync(userId, media.Id, request.Progress);

        logger.LogInformation("Book tracking for user {UserId}, '{Title}'", userId, media.TitleEN ?? media.TitleES);

        // Runs after the response is sent, so the client never waits on Open Library.
        if (MediaService.NeedsOpenLibraryEnrichment(media))
        {
            var (mediaId, title, author, isbn) = (media.Id, request.Title, request.Author, request.Isbn);
            background.Run<MediaService>($"OpenLibrary enrichment for media {mediaId}",
                mediaService => mediaService.EnrichFromOpenLibraryAsync(mediaId, title, author, isbn));
        }

        return Results.Ok(new { success = true, message = "Tracking event added successfully" });
    }
}
