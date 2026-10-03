using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Review.GetReviews;

/// <summary>
/// <c>GET /api/reviews</c>: every review the caller has written, with a summary of each media.
/// Requires a valid JWT.
/// Lists the caller's reviews, newest first. Returns 401 if the token has no valid user id.
/// </summary>
public static class GetReviewsEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, behind the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/reviews", HandleAsync)
            .WithName("GetReviews")
            .WithTags("Reviews")
            .WithSummary("List the caller's reviews")
            .Responds<GetReviewsResponse>("The caller's reviews with a summary of each media.")
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>List the caller's reviews</summary>
    private static async Task<IResult> HandleAsync(
        ReviewService reviewService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var reviews = await reviewService.GetByUserIdAsync(userId);

        var data = reviews
            .Select(r => new ReviewWithMediaItem
            {
                Id = r.Id,
                MediaId = r.MediaId,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                Media = new ReviewMediaSummary
                {
                    Id = r.Media?.Id,
                    TitleEN = r.Media?.TitleEN,
                    TitleES = r.Media?.TitleES,
                    Type = r.Media?.Type.ToString(),
                    PosterUrl = r.Media?.PosterUrl,
                    Author = r.Media?.Author,
                }
            })
            .ToList();

        return Results.Ok(new GetReviewsResponse { Success = true, Count = data.Count, Data = data });
    }
}
