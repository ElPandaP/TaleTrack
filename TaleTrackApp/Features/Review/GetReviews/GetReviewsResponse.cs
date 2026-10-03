namespace TaleTrackApp.Features.Review.GetReviews;

/// <summary>Body returned by <c>GET /api/reviews</c>.</summary>
public class GetReviewsResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>Number of reviews in `data`.</summary>
    public int Count { get; set; }
    /// <summary>The caller's reviews.</summary>
    public required List<ReviewWithMediaItem> Data { get; set; }
}

/// <summary>One of the caller's reviews, with the media it is about.</summary>
public class ReviewWithMediaItem
{
    /// <summary>Review id.</summary>
    public Guid Id { get; set; }
    /// <summary>The reviewed media.</summary>
    public Guid MediaId { get; set; }
    /// <summary>Rating from 1 to 10.</summary>
    public int Rating { get; set; }
    /// <summary>Free-text comment, if any.</summary>
    public string? Comment { get; set; }
    /// <summary>When the review was written.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>When the review was last edited. Null if never.</summary>
    public DateTime? UpdatedAt { get; set; }
    /// <summary>Summary of the reviewed media.</summary>
    public required ReviewMediaSummary Media { get; set; }
}

/// <summary>The few media fields needed to show a review in a list.</summary>
public class ReviewMediaSummary
{
    /// <summary>Media id.</summary>
    public Guid? Id { get; set; }
    /// <summary>English title, if known.</summary>
    public string? TitleEN { get; set; }
    /// <summary>Spanish title, if known.</summary>
    public string? TitleES { get; set; }
    /// <summary>`Movie`, `Series` or `Book`.</summary>
    public string? Type { get; set; }
    /// <summary>Poster or cover image URL, if known.</summary>
    public string? PosterUrl { get; set; }
    /// <summary>Author of a book.</summary>
    public string? Author { get; set; }
}
