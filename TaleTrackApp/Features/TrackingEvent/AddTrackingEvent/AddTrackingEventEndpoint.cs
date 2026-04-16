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
        ClaimsPrincipal user,
        ILogger<AddTrackingEventRequest> logger)
    {
        // Obtener el UserId del JWT
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        // Process: Buscar o crear el Media
        try
        {
            var media = await mediaService.FindOrCreateAsync(request.Title, request.Type, request.Length);

            // Crear el TrackingEvent
            var trackingEvent = await trackingEventService.CreateAsync(
                userId,
                media.Id,
                request.Progress
            );

            logger.LogInformation($"TrackingEvent created for user {userId}, media '{media.Title}'");
            return Results.Ok(new { success = true, message = "Tracking event agregado exitosamente" });
        }
        catch (Exception ex)
        {
            logger.LogError($"Error creating tracking event: {ex.Message}");
            return Results.StatusCode(500);
        }
    }
}
