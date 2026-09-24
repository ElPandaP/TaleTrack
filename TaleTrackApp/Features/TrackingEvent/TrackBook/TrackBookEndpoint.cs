using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Security;
using TaleTrackApp.Services;

namespace TaleTrackApp.Features.TrackingEvent.TrackBook;

public static class TrackBookEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/tracking/books", HandleAsync)
            .WithName("TrackBook")
            .WithTags("Tracking")
            .WithSummary("Record reading progress on a book")
            .WithDescription("Used by the KOReader plugin. The book is matched by ISBN, then by title and author, then by title alone, and created if unknown. Progress never goes down through this endpoint.")
            .Responds<ApiResult>("Progress recorded.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        TrackBookRequest request,
        MediaService mediaService,
        TrackingEventService trackingEventService,
        BackgroundRunner background,
        ClaimsPrincipal user,
        ILogger<TrackBookRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        try
        {
            var media = await mediaService.FindOrCreateAsync(
                request.Title, Model.MediaType.Book, request.Pages ?? 0, request.Author, request.Isbn);

            await trackingEventService.UpsertAsync(userId, media.Id, request.Progress);

            logger.LogInformation("Book tracking for user {UserId}, '{Title}'", userId, media.TitleEN ?? media.TitleES);

            // Fire-and-forget enrichment — KOReader does not wait for this.
            if (MediaService.NeedsOpenLibraryEnrichment(media))
            {
                var (mediaId, title, author, isbn) = (media.Id, request.Title, request.Author, request.Isbn);
                background.Run<MediaService>($"OpenLibrary enrichment for media {mediaId}",
                    mediaService => mediaService.EnrichFromOpenLibraryAsync(mediaId, title, author, isbn));
            }

            return Results.Ok(new { success = true, message = "Tracking event added successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating book tracking event");
            return Results.StatusCode(500);
        }
    }
}
