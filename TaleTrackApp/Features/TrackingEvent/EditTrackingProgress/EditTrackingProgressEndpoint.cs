using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.TrackingEvent.EditTrackingProgress;

/// <summary>
/// <c>PUT /api/tracking/{mediaId}</c>: manual correction of the caller's progress on a media, used
/// from the library. Requires a valid JWT.
/// Applies either a percentage or, for a series, a season and episode. Returns 400 when neither is given, 404 when there is no tracking to update and 401 if the token has no valid user id.
/// </summary>
public static class EditTrackingProgressEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, with request validation and the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/tracking/{mediaId:guid}", HandleAsync)
            .WithName("EditTrackingProgress")
            .WithTags("Tracking")
            .WithSummary("Correct the caller's progress on a media")
            .WithDescription("Manual correction from the library. Send either `progress` (a percentage) or, for a series, `season` and `episode` to mark that episode as reached.")
            .Responds<EditTrackingProgressResponse>("Progress updated.")
            .RespondsBadRequest("Neither `progress` nor both `season` and `episode` were given, or validation failed.")
            .RespondsNotFound("The caller has no tracking for this media.")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Correct the caller's progress on a media</summary>
    private static async Task<IResult> HandleAsync(
        Guid mediaId,
        EditTrackingProgressRequest request,
        TrackingEventService trackingEventService,
        ClaimsPrincipal user,
        ILogger<EditTrackingProgressRequest> logger)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

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
}
