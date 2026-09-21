using System.Security.Claims;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Review.EditReview;

public static class EditReviewEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/reviews/{id:guid}", HandleAsync)
            .WithName("EditReview")
            .WithDescription("Edits a review")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        EditReviewRequest request,
        ReviewService reviewService,
        ClaimsPrincipal user,
        ILogger<EditReviewRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var (result, updatedReview) = await reviewService.UpdateAsync(userId, id, request.Rating, request.Comment);
            switch (result)
            {
                case ReviewResult.NotFound:
                    return Results.NotFound(new { success = false, message = "Review not found" });
                case ReviewResult.Forbidden:
                    return Results.Forbid();
            }

            logger.LogInformation($"Review {id} updated by user {userId}");
            return Results.Ok(new 
            { 
                success = true, 
                message = "Review updated successfully",
                data = new
                {
                    id = updatedReview!.Id,
                    userId = updatedReview.UserId,
                    mediaId = updatedReview.MediaId,
                    rating = updatedReview.Rating,
                    comment = updatedReview.Comment,
                    updatedAt = updatedReview.UpdatedAt
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError($"Error updating review: {ex.Message}");
            return Results.StatusCode(500);
        }
    }
}
