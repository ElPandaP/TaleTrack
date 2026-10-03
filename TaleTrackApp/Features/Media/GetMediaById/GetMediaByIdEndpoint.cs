using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Features.TrackingEvent;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Media.GetMediaById;

/// <summary>
/// <c>GET /api/media/{id}</c>: the detail page of a media, with the caller's own progress and
/// review plus every user's reviews. Requires a valid JWT.
/// Loads the media, its reviews and the caller's tracking, and shapes them into the detail response. Returns 404 if the media does not exist and 401 if the token has no valid user id.
/// </summary>
public static class GetMediaByIdEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, behind the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/media/{id:guid}", HandleAsync)
            .WithName("GetMediaById")
            .WithTags("Media")
            .WithSummary("Get a media's detail page")
            .WithDescription("Media data, the caller's own progress and review, and every user's reviews. Fields prefixed `my` are absent when the caller has not tracked or reviewed it.")
            .Responds<GetMediaByIdResponse>("The media detail.")
            .RespondsNotFound("The media does not exist.")
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Get a media's detail page</summary>
    private static async Task<IResult> HandleAsync(
        Guid id,
        MediaService mediaService,
        ReviewService reviewService,
        TrackingEventService trackingEventService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var media = await mediaService.GetByIdAsync(id);
        if (media == null)
            return Results.NotFound(new { success = false, message = "Media not found" });

        var reviews = await reviewService.GetByMediaIdAsync(id);
        var myTracking = await trackingEventService.GetForMediaAsync(userId, id);
        var myProgress = SeriesProgressCalculator.ProgressFor(media, myTracking);

        var detail = new MediaDetail(media, reviews, reviews.FirstOrDefault(r => r.UserId == userId), myTracking, myProgress);
        return Results.Ok(GetMediaByIdResponse.From(detail, userId));
    }
}
