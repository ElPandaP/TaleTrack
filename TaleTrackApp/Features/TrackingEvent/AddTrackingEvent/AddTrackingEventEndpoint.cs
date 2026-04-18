using System.Security.Claims;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.TrackingEvent.AddTrackingEvent;

public static class AddTrackingEventEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/tracking", HandleAsync)
            .WithName("AddTrackingEvent")
            .WithDescription("Agrega un evento de tracking para el usuario autenticado")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        AddTrackingEventRequest request,
        MediaService mediaService,
        TrackingEventService trackingEventService,
        OpenLibraryService openLibraryService,
        IServiceScopeFactory scopeFactory,
        ClaimsPrincipal user,
        ILogger<AddTrackingEventRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var media = await mediaService.FindOrCreateAsync(
                request.Title, request.Type, request.Length, request.Author, request.Isbn);

            var trackingEvent = await trackingEventService.CreateAsync(userId, media.Id, request.Progress);

            logger.LogInformation("TrackingEvent created for user {UserId}, media '{Title}'", userId, media.Title);

            // Fire-and-forget enrichment — KOReader does not wait for this
            if (request.Type == "Book" &&
                (string.IsNullOrWhiteSpace(media.PosterUrl) || string.IsNullOrWhiteSpace(media.Author)))
            {
                var (mediaId, title, author, isbn) = (media.Id, request.Title, request.Author, request.Isbn);
                _ = Task.Run(async () =>
                {
                    // Fresh scope so the background task gets its own DbContext
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var bgMedia = scope.ServiceProvider.GetRequiredService<MediaService>();
                    var bgOL    = scope.ServiceProvider.GetRequiredService<OpenLibraryService>();
                    var bgLog   = scope.ServiceProvider.GetRequiredService<ILogger<AddTrackingEventRequest>>();
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
            logger.LogError(ex, "Error creating tracking event");
            return Results.StatusCode(500);
        }
    }
}
