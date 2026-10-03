using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Review.EditReview;

/// <summary>
/// <c>PUT /api/reviews/{id}</c>: edits one of the caller's reviews. Requires a valid JWT.
/// Updates the rating and, when given, the comment, and returns the review. Returns 404 if it does not exist, 403 if it belongs to someone else and 401 if the token has no valid user id.
/// </summary>
public static class EditReviewEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, with request validation and the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/reviews/{id:guid}", HandleAsync)
            .WithName("EditReview")
            .WithTags("Reviews")
            .WithSummary("Edit a review")
            .Responds<EditReviewResponse>("Review updated.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .Responds(StatusCodes.Status403Forbidden, "The review belongs to someone else.")
            .RespondsNotFound("The review does not exist.")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Edit a review</summary>
    private static async Task<IResult> HandleAsync(
        Guid id,
        EditReviewRequest request,
        ReviewService reviewService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var (result, updatedReview) = await reviewService.UpdateAsync(userId, id, request.Rating, request.Comment);
        switch (result)
        {
            case ReviewResult.NotFound:
                return Results.NotFound(new { success = false, message = "Review not found" });
            case ReviewResult.Forbidden:
                return Results.Forbid();
        }

        return Results.Ok(new EditReviewResponse
        {
            Success = true,
            Message = "Review updated successfully",
            Data = new EditedReviewData
            {
                Id = updatedReview!.Id,
                UserId = updatedReview.UserId,
                MediaId = updatedReview.MediaId,
                Rating = updatedReview.Rating,
                Comment = updatedReview.Comment,
                UpdatedAt = updatedReview.UpdatedAt,
            }
        });
    }
}
