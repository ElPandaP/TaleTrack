namespace TaleTrackApp.Features.Review.AddReview;

using System.ComponentModel.DataAnnotations;

public class AddReviewRequest
{
    [Required(ErrorMessage = "MediaId es requerido")]
    [Range(1, int.MaxValue, ErrorMessage = "MediaId debe ser mayor que 0")]
    public int MediaId { get; set; }
    
    [Required(ErrorMessage = "Rating es requerido")]
    [Range(1, 10, ErrorMessage = "Rating debe estar entre 1 y 10")]
    public int Rating { get; set; }
    
    [StringLength(2000, ErrorMessage = "Comment no puede exceder 2000 caracteres")]
    public string? Comment { get; set; }
}
