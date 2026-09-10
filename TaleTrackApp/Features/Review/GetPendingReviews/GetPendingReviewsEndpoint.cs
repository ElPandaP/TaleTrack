using System.Security.Claims;
using TaleTrackApp.Features.Library;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.Review.GetPendingReviews;

public static class GetPendingReviewsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/reviews/pending", HandleAsync)
            .WithName("GetPendingReviews")
            .WithDescription("Media the user has finished but not yet reviewed")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        LibraryService libraryService,
        ClaimsPrincipal user,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(GetPendingReviewsEndpoint));

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var pending = await libraryService.GetPendingReviewsAsync(userId);
            return Results.Ok(new { success = true, count = pending.Count, data = pending });
        }
        catch (Exception ex)
        {
            logger.LogError("Error retrieving pending reviews: {Message}", ex.Message);
            return Results.StatusCode(500);
        }
    }
}
