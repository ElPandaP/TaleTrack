using System.Security.Claims;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Review.DeleteReview;

public static class DeleteReviewEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/reviews/{id:guid}", HandleAsync)
            .WithName("DeleteReview")
            .WithDescription("Deletes a review")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        ReviewService reviewService,
        ClaimsPrincipal user,
        ILogger<Guid> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var result = await reviewService.DeleteAsync(userId, id);
            switch (result)
            {
                case ReviewResult.NotFound:
                    return Results.NotFound(new { success = false, message = "Review not found" });
                case ReviewResult.Forbidden:
                    return Results.Forbid();
            }

            logger.LogInformation($"Review {id} deleted by user {userId}");
            return Results.Ok(new { success = true, message = "Review deleted successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError($"Error deleting review: {ex.Message}");
            return Results.StatusCode(500);
        }
    }
}
