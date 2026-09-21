using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.TrackingEvent.EditTrackingProgress;

public static class EditTrackingProgressEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/tracking/{mediaId:guid}", HandleAsync)
            .WithName("EditTrackingProgress")
            .WithDescription("Updates the progress of the user's most recent tracking event for a media")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        Guid mediaId,
        EditTrackingProgressRequest request,
        TrackingEventService trackingEventService,
        ClaimsPrincipal user,
        ILogger<EditTrackingProgressRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            if (!(request.Season.HasValue && request.Episode.HasValue) && !request.Progress.HasValue)
                return Results.BadRequest(new { success = false, message = "Provide either progress or season and episode" });

            var updated = await trackingEventService.EditProgressAsync(
                userId, mediaId, request.Progress, request.Season, request.Episode);

            if (updated == null)
                return Results.NotFound(new { success = false, message = "No tracking found for this media" });

            logger.LogInformation("Progress for media {MediaId} updated by user {UserId}", mediaId, userId);
            return Results.Ok(new
            {
                success = true,
                message = "Progress updated successfully",
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
