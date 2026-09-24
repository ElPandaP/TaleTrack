using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.TrackingEvent.EditTrackingProgress;

public static class EditTrackingProgressEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/tracking/{mediaId:guid}", HandleAsync)
            .WithName("EditTrackingProgress")
            .WithTags("Tracking")
            .WithSummary("Correct the caller's progress on a media")
            .WithDescription("Manual correction, so unlike the track endpoints it can lower progress. Send either `progress` (a percentage) or, for a series, `season` and `episode` to mark that episode as reached.")
            .Responds<EditTrackingProgressResponse>("Progress updated.")
            .RespondsBadRequest("Neither `progress` nor both `season` and `episode` were given, or validation failed.")
            .RespondsNotFound("The caller has no tracking for this media.")
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
            return Results.Ok(new EditTrackingProgressResponse
            {
                Success = true,
                Message = "Progress updated successfully",
                Data = new EditedProgressData { Progress = updated.Progress },
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating progress for media {MediaId}", mediaId);
            return Results.StatusCode(500);
        }
    }
}
