namespace MediaTrackerApp.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Review
{
    [Key]
    public int Id { get; set; }
    
    [Required(ErrorMessage = "UserId is required")]
    [Range(1, int.MaxValue, ErrorMessage = "UserId must be a valid positive integer")]
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }
    
    [Required(ErrorMessage = "MediaId is required")]
    [Range(1, int.MaxValue, ErrorMessage = "MediaId must be a valid positive integer")]
    [ForeignKey(nameof(Media))]
    public int MediaId { get; set; }
    
    [StringLength(2000, ErrorMessage = "Comment cannot exceed 2000 characters")]
    public string? Comment { get; set; }
    
    [Required(ErrorMessage = "Rating is required")]
    [Range(1, 10, ErrorMessage = "Rating must be between 1 and 10")]
    public int Rating { get; set; }
    
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public User? User { get; set; }
    public Media? Media { get; set; }
}
