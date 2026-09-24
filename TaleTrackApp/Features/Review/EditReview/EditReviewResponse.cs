namespace TaleTrackApp.Features.Review.EditReview;

public class EditReviewResponse
{
    /// <summary>Always <c>true</c>.</summary>
    public bool Success { get; set; }

    /// <summary>Human-readable confirmation in English.</summary>
    public required string Message { get; set; }

    /// <summary>The review after the update.</summary>
    public required EditedReviewData Data { get; set; }
}

public class EditedReviewData
{
    /// <summary>Review id.</summary>
    public Guid Id { get; set; }

    /// <summary>Author of the review (the caller).</summary>
    public Guid UserId { get; set; }

    /// <summary>The media that was reviewed.</summary>
    public Guid MediaId { get; set; }

    /// <summary>Rating from 1 to 10.</summary>
    public int Rating { get; set; }

    /// <summary>Free-text comment, if any.</summary>
    public string? Comment { get; set; }

    /// <summary>When the review was last edited.</summary>
    public DateTime? UpdatedAt { get; set; }
}
