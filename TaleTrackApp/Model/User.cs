namespace TaleTrackApp.Model;

using System.ComponentModel.DataAnnotations;

public class User
{
    [Key]
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
    public required string Email { get; set; }
    
    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    public required string Username { get; set; }
    
    [StringLength(512, ErrorMessage = "Password hash cannot exceed 512 characters")]
    public string? PasswordHash { get; set; }

    [StringLength(255, ErrorMessage = "Google ID cannot exceed 255 characters")]
    public string? GoogleId { get; set; }
    
    [StringLength(512, ErrorMessage = "Refresh token cannot exceed 512 characters")]
    public string? RefreshToken { get; set; }
    
    public DateTime? RefreshTokenExpiry { get; set; }
    
    public DateTime? LastLogin { get; set; }
    
    [Required]
    public bool IsActive { get; set; } = true;
    
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<TrackingEvent> TrackingEvents { get; set; } = new List<TrackingEvent>();
}
