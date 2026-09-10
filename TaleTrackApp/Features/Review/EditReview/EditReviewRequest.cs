namespace TaleTrackApp.Features.Review.EditReview;

using System.ComponentModel.DataAnnotations;

public class EditReviewRequest
{
    [Required(ErrorMessage = "Rating is required")]
    [Range(1, 10, ErrorMessage = "Rating must be between 1 and 10")]
    public int Rating { get; set; }
    
    [StringLength(2000, ErrorMessage = "Comment cannot exceed 2000 characters")]
    public string? Comment { get; set; }
}
