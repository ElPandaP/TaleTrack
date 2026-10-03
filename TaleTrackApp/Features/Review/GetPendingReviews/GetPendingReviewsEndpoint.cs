using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Library;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Review.GetPendingReviews;

/// <summary>
/// <c>GET /api/reviews/pending</c>: media the caller has finished but not reviewed yet. Requires a
/// valid JWT.
/// Lists the finished, unreviewed media, most recent first. Returns 401 if the token has no valid user id.
/// </summary>
public static class GetPendingReviewsEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, behind the user policy.</summary>
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

    /// <summary>List finished media awaiting a review</summary>
    private static async Task<IResult> HandleAsync(
        LibraryService libraryService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var pending = await libraryService.GetPendingReviewsAsync(userId);
        return Results.Ok(new GetPendingReviewsResponse { Success = true, Count = pending.Count, Data = pending });
    }
}
