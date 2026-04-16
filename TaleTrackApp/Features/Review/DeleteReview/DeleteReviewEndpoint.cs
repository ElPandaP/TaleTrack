using System.Security.Claims;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.Review.DeleteReview;

public static class DeleteReviewEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/review/{id}", HandleAsync)
            .WithName("DeleteReview")
            .WithDescription("Elimina una reseña (requiere JWT + API Key interna)")
            .RequireAuthorization(Policies.UserPolicy)
            .RequireAuthorization(Policies.InternalOnly);
    }

    private static async Task<IResult> HandleAsync(
        int id,
        ReviewService reviewService,
        ClaimsPrincipal user,
        ILogger<int> logger)
    {
        // Obtener el UserId del JWT
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var review = await reviewService.GetByIdAsync(id);
            
            if (review == null)
            {
                return Results.NotFound(new { success = false, message = "Reseña no encontrada" });
            }

            // Solo el dueño de la reseña puede eliminarla
            if (review.UserId != userId)
            {
                logger.LogWarning($"User {userId} tried to delete review {id} owned by {review.UserId}");
                return Results.Forbid();
            }

            var success = await reviewService.DeleteAsync(id);

            if (!success)
            {
                return Results.NotFound(new { success = false, message = "Reseña no encontrada" });
            }

            logger.LogInformation($"Review {id} deleted by user {userId}");
            return Results.Ok(new { success = true, message = "Reseña eliminada exitosamente" });
        }
        catch (Exception ex)
        {
            logger.LogError($"Error deleting review: {ex.Message}");
            return Results.StatusCode(500);
        }
    }
}
