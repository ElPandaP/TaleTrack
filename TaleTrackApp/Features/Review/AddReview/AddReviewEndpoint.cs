using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Review.AddReview;

public static class AddReviewEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/reviews", HandleAsync)
            .WithName("AddReview")
            .WithTags("Reviews")
            .WithSummary("Review a media")
            .WithDescription("One review per user and media: reviewing a media again updates the existing review instead of creating a second one.")
            .Responds<AddReviewResponse>("Review saved.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        AddReviewRequest request,
        ReviewService reviewService,
        ClaimsPrincipal user,
        ILogger<AddReviewRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var review = await reviewService.CreateAsync(userId, request.MediaId!.Value, request.Rating, request.Comment);

            logger.LogInformation($"Review created by user {userId} for media {request.MediaId}");
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
        catch (Exception ex)
        {
            logger.LogError($"Error creating review: {ex.Message}");
            return Results.StatusCode(500);
        }
    }
}
