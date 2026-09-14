namespace TaleTrackApp.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Review
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [Required(ErrorMessage = "UserId is required")]
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }

    [Required(ErrorMessage = "MediaId is required")]
    [ForeignKey(nameof(Media))]
    public Guid MediaId { get; set; }
    
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
