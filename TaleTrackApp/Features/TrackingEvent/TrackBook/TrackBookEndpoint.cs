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
            .WithDescription("Records reading progress for a book (used by the KOReader plugin)")
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
                request.Title, "Book", request.Pages ?? 0, request.Author, request.Isbn);

            await trackingEventService.UpsertAsync(userId, media.Id, request.Progress);

            logger.LogInformation("Book tracking for user {UserId}, '{Title}'", userId, media.Title);

            // Fire-and-forget enrichment — KOReader does not wait for this.
            if (string.IsNullOrWhiteSpace(media.PosterUrl) || string.IsNullOrWhiteSpace(media.Author))
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
