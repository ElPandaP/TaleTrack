namespace TaleTrackApp.Features.Review.EditReview;

using System.ComponentModel.DataAnnotations;

public class EditReviewRequest
{
    /// <summary>From 1 (worst) to 10 (best).</summary>
    [Required(ErrorMessage = "Rating is required")]
    [Range(1, 10, ErrorMessage = "Rating must be between 1 and 10")]
    public int Rating { get; set; }
    
    /// <summary>Optional comment, up to 2000 characters. Omitting it clears the current one.</summary>
    [StringLength(2000, ErrorMessage = "Comment cannot exceed 2000 characters")]
    public string? Comment { get; set; }
}
