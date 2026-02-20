namespace MediaTrackerApp.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class TrackingEvent
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
    
    [Required(ErrorMessage = "EventType is required")]
    [StringLength(20, ErrorMessage = "EventType cannot exceed 20 characters")]
    [RegularExpression(@"^(Movie|Series|Book)$", ErrorMessage = "EventType must be 'Movie', 'Series', or 'Book'")]
    public required string EventType { get; set; }
    
    [Range(0, 100, ErrorMessage = "Progress must be between 0 and 100")]
    public int? Progress { get; set; }
    
    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? Notes { get; set; }
    
    [Required]
    public DateTime EventDate { get; set; } = DateTime.UtcNow;
    
    public User? User { get; set; }
    public Media? Media { get; set; }
}
