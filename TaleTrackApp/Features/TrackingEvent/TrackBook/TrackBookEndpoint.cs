using System.Security.Claims;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Auth;

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
        IServiceScopeFactory scopeFactory,
        ClaimsPrincipal user,
        ILogger<TrackBookRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
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
                _ = Task.Run(async () =>
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var bgMedia = scope.ServiceProvider.GetRequiredService<MediaService>();
                    var bgOL = scope.ServiceProvider.GetRequiredService<OpenLibraryService>();
                    var bgLog = scope.ServiceProvider.GetRequiredService<ILogger<TrackBookRequest>>();
                    try
                    {
                        var result = await bgOL.EnrichAsync(title, author, isbn);
                        if (result != null)
                            await bgMedia.ApplyEnrichmentAsync(mediaId, result);
                    }
                    catch (Exception ex)
                    {
                        bgLog.LogError(ex, "Background enrichment failed for mediaId={Id}", mediaId);
                    }
                });
            }

            return Results.Ok(new { success = true, message = "Tracking event agregado exitosamente" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating book tracking event");
            return Results.StatusCode(500);
        }
    }
}
