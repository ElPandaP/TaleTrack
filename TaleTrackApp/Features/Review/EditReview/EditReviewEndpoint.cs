using System.Security.Claims;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.Review.EditReview;

public static class EditReviewEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/review/{id}", HandleAsync)
            .WithName("EditReview")
            .WithDescription("Edita una reseña (requiere JWT + API Key interna)")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy)
            .RequireAuthorization(Policies.InternalOnly);
    }

    private static async Task<IResult> HandleAsync(
        int id,
        EditReviewRequest request,
        ReviewService reviewService,
        ClaimsPrincipal user,
        ILogger<EditReviewRequest> logger)
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

            // Solo el dueño de la reseña puede editarla
            if (review.UserId != userId)
            {
                logger.LogWarning($"User {userId} tried to edit review {id} owned by {review.UserId}");
                return Results.Forbid();
            }

            var updatedReview = await reviewService.UpdateAsync(id, request.Rating, request.Comment);

            logger.LogInformation($"Review {id} updated by user {userId}");
            return Results.Ok(new 
            { 
                success = true, 
                message = "Reseña actualizada exitosamente",
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
