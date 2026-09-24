using TaleTrackApp.Features.Library;

namespace TaleTrackApp.Features.Review.GetPendingReviews;

public class GetPendingReviewsResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>Rows in `data`.</summary>
    public int Count { get; set; }
    /// <summary>Finished media without a review, most recent first.</summary>
    public required List<LibraryItem> Data { get; set; }
}
