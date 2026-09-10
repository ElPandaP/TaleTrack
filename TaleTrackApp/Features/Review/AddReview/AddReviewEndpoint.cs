using System.Security.Claims;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.Review.AddReview;

public static class AddReviewEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/review", HandleAsync)
            .WithName("AddReview")
            .WithDescription("Adds a review")
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
        
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var review = await reviewService.CreateAsync(userId, request.MediaId, request.Rating, request.Comment);

            logger.LogInformation($"Review created by user {userId} for media {request.MediaId}");
            return Results.Ok(new 
            { 
                success = true, 
                message = "Review added successfully",
                data = new
                {
                    id = review.Id,
                    userId = review.UserId,
                    mediaId = review.MediaId,
                    rating = review.Rating,
                    comment = review.Comment,
                    createdAt = review.CreatedAt
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
