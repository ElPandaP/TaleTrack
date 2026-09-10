using System.Security.Claims;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.TrackingEvent.DeleteTracking;

public static class DeleteTrackingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/tracking/{mediaId}", HandleAsync)
            .WithName("DeleteTracking")
            .WithDescription("Deletes all of the user's tracking for a media (removes it from their library)")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        int mediaId,
        TrackingEventService trackingEventService,
        ClaimsPrincipal user,
        ILogger<int> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var deleted = await trackingEventService.DeleteAllForMediaAsync(userId, mediaId);

            if (!deleted)
                return Results.NotFound(new { success = false, message = "No tracking found for this media" });

            logger.LogInformation("Tracking for media {MediaId} deleted by user {UserId}", mediaId, userId);
            return Results.Ok(new { success = true, message = "Tracking deleted successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting tracking for media {MediaId}", mediaId);
            return Results.StatusCode(500);
        }
    }
}
