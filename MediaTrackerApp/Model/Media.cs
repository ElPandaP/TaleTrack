namespace MediaTrackerApp.Model;

using System.ComponentModel.DataAnnotations;

public class Media
{
    [Key]
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Title is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 255 characters")]
    public required string Title { get; set; }
    
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }
    
    [Required(ErrorMessage = "Type is required")]
    [StringLength(20, ErrorMessage = "Type cannot exceed 20 characters")]
    [RegularExpression(@"^(Movie|Series|Book)$", ErrorMessage = "Type must be 'Movie', 'Series', or 'Book'")]
    public required string Type { get; set; }
    
    [Range(1, int.MaxValue, ErrorMessage = "Length must be greater than 0")]
    public int Length { get; set; }
    
    [StringLength(500, ErrorMessage = "External ID cannot exceed 500 characters")]
    public string? ExternalId { get; set; }
    
    [Url(ErrorMessage = "Invalid URL format for PosterUrl")]
    [StringLength(2048, ErrorMessage = "PosterUrl cannot exceed 2048 characters")]
    public string? PosterUrl { get; set; }
    
    [Required]
    public DateTime FirstTrackedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<TrackingEvent> TrackingEvents { get; set; } = new List<TrackingEvent>();
}