using TaleTrackApp.Features.Library;

namespace TaleTrackApp.Features.Review.GetPendingReviews;

public class GetPendingReviewsResponse
{
    public bool Success { get; set; }
    public int Count { get; set; }
    public required List<LibraryItem> Data { get; set; }
}
