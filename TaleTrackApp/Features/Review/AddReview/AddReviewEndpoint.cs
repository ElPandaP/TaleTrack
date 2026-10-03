using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.TrackingEvent;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Review.AddReview;

/// <summary>
/// <c>POST /api/reviews</c>: reviews a media in the caller's library. Reviewing the same media again
/// overwrites the caller's existing review. Requires a valid JWT.
/// Creates or overwrites the caller's review of the media and returns it. Returns 404 if the caller does not track the media (which includes a media that does not exist) and 401 if the token has no valid user id.
/// </summary>
public static class AddReviewEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, with request validation and the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/reviews", HandleAsync)
            .WithName("AddReview")
            .WithTags("Reviews")
            .WithSummary("Review a media")
            .WithDescription("Only media in the caller's library can be reviewed. One review per user and media: reviewing a media again updates the existing review instead of creating a second one.")
            .Responds<AddReviewResponse>("Review saved.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .RespondsNotFound("The caller does not track the media, or it does not exist.")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Review a media</summary>
    private static async Task<IResult> HandleAsync(
        AddReviewRequest request,
        ReviewService reviewService,
        TrackingEventService trackingService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        if (await trackingService.GetForMediaAsync(userId, request.MediaId!.Value) == null)
            return Results.NotFound(new { success = false, message = "Media not in your library" });

        var review = await reviewService.CreateAsync(userId, request.MediaId!.Value, request.Rating, request.Comment);

        return Results.Ok(new AddReviewResponse
        {
            Success = true,
            Message = "Review added successfully",
            Data = new AddedReviewData
            {
                Id = review.Id,
                UserId = review.UserId,
                MediaId = review.MediaId,
                Rating = review.Rating,
                Comment = review.Comment,
                CreatedAt = review.CreatedAt,
            }
        });
    }
}
