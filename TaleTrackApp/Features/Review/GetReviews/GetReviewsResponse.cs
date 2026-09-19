namespace TaleTrackApp.Features.Review.GetReviews;

public class GetReviewsResponse
{
    public bool Success { get; set; }
    public int Count { get; set; }
    public required List<ReviewWithMediaItem> Data { get; set; }
}

public class ReviewWithMediaItem
{
    public Guid Id { get; set; }
    public Guid MediaId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public required ReviewMediaSummary Media { get; set; }
}

public class ReviewMediaSummary
{
    public Guid? Id { get; set; }
    public string? TitleEN { get; set; }
    public string? TitleES { get; set; }
    public string? Type { get; set; }
    public string? PosterUrl { get; set; }
    public string? Author { get; set; }
}
