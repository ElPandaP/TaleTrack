using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Review.DeleteReview;

/// <summary>
/// <c>DELETE /api/reviews/{id}</c>: deletes one of the caller's reviews. Requires a valid JWT.
/// Deletes the review if the caller wrote it. Returns 404 if it does not exist, 403 if it belongs to someone else and 401 if the token has no valid user id.
/// </summary>
public static class DeleteReviewEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, behind the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/reviews/{id:guid}", HandleAsync)
            .WithName("DeleteReview")
            .WithTags("Reviews")
            .WithSummary("Delete a review")
            .Responds<ApiResult>("Review deleted.")
            .Responds(StatusCodes.Status403Forbidden, "The review belongs to someone else.")
            .RespondsNotFound("The review does not exist.")
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Delete a review</summary>
    private static async Task<IResult> HandleAsync(
        Guid id,
        ReviewService reviewService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var result = await reviewService.DeleteAsync(userId, id);
        switch (result)
        {
            case ReviewResult.NotFound:
                return Results.NotFound(new { success = false, message = "Review not found" });
            case ReviewResult.Forbidden:
                return Results.Forbid();
        }

        return Results.Ok(new { success = true, message = "Review deleted successfully" });
    }
}
