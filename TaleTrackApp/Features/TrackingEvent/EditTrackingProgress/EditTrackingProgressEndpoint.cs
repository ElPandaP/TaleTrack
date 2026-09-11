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
            Model.TrackingEvent? updated;
            if (request.Season.HasValue && request.Episode.HasValue)
            {
                // Series: log the furthest episode reached as fully watched. Earlier episodes
                // are assumed watched too — the overall % is derived from this at read time.
                updated = await trackingEventService.UpsertAsync(
                    userId, mediaId, progress: 100, request.Season, request.Episode);
            }
            else if (request.Progress.HasValue)
            {
                updated = await trackingEventService.SetProgressAsync(userId, mediaId, request.Progress.Value);
            }
            else
            {
                return Results.BadRequest(new { success = false, message = "Provide either progress or season and episode" });
            }

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
