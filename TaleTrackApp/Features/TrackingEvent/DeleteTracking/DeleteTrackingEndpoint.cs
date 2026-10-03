using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.TrackingEvent.DeleteTracking;

/// <summary>
/// <c>DELETE /api/tracking/{mediaId}</c>: stops tracking a media, which removes it from the
/// caller's library. The media itself and any review stay. Requires a valid JWT.
/// Deletes the caller's tracking of the media. Returns 404 if they were not tracking it and 401 if the token has no valid user id.
/// </summary>
public static class DeleteTrackingEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, behind the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/tracking/{mediaId:guid}", HandleAsync)
            .WithName("DeleteTracking")
            .WithTags("Tracking")
            .WithSummary("Stop tracking a media")
            .WithDescription("Deletes the caller's tracking, which takes the media out of their library. The media and any review stay.")
            .Responds<ApiResult>("Tracking deleted.")
            .RespondsNotFound("The caller has no tracking for this media.")
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Stop tracking a media</summary>
    private static async Task<IResult> HandleAsync(
        Guid mediaId,
        TrackingEventService trackingEventService,
        ClaimsPrincipal user,
        ILogger<Guid> logger)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var deleted = await trackingEventService.DeleteForMediaAsync(userId, mediaId);

        if (!deleted)
            return Results.NotFound(new { success = false, message = "No tracking found for this media" });

        logger.LogInformation("Tracking for media {MediaId} deleted by user {UserId}", mediaId, userId);
        return Results.Ok(new { success = true, message = "Tracking deleted successfully" });
    }
}
