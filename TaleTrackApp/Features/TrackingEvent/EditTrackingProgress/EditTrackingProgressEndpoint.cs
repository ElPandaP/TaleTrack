using System.Security.Claims;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.TrackingEvent.EditTrackingProgress;

public static class EditTrackingProgressEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/tracking/{mediaId}", HandleAsync)
            .WithName("EditTrackingProgress")
            .WithDescription("Updates the progress of the user's most recent tracking event for a media")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        int mediaId,
        EditTrackingProgressRequest request,
        TrackingEventService trackingEventService,
        ClaimsPrincipal user,
        ILogger<EditTrackingProgressRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var updated = await trackingEventService.SetProgressAsync(userId, mediaId, request.Progress);

            if (updated == null)
                return Results.NotFound(new { success = false, message = "No se encontró seguimiento para este contenido" });

            logger.LogInformation("Progress for media {MediaId} set to {Progress} by user {UserId}",
                mediaId, request.Progress, userId);
            return Results.Ok(new
            {
                success = true,
                message = "Progreso actualizado exitosamente",
                data = new { progress = updated.Progress }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating progress for media {MediaId}", mediaId);
            return Results.StatusCode(500);
        }
    }
}
