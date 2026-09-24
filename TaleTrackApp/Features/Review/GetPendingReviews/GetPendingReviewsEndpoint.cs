using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Library;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Review.GetPendingReviews;

public static class GetPendingReviewsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/reviews/pending", HandleAsync)
            .WithName("GetPendingReviews")
            .WithTags("Reviews")
            .WithSummary("List finished media awaiting a review")
            .WithDescription("Media the caller has finished (100% progress) but not reviewed yet, most recent first.")
            .Responds<GetPendingReviewsResponse>("The media to review.")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        LibraryService libraryService,
        ClaimsPrincipal user,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(GetPendingReviewsEndpoint));

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var pending = await libraryService.GetPendingReviewsAsync(userId);
            return Results.Ok(new GetPendingReviewsResponse { Success = true, Count = pending.Count, Data = pending });
        }
        catch (Exception ex)
        {
            logger.LogError("Error retrieving pending reviews: {Message}", ex.Message);
            return Results.StatusCode(500);
        }
    }
}
