namespace TaleTrackApp.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// A user's rating (1 to 10) and optional comment on a media. There is at most one review per
/// user and media.
/// </summary>
public class Review
{
    /// <summary>Primary key.</summary>
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Author of the review.</summary>
    [Required(ErrorMessage = "UserId is required")]
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }

    /// <summary>Reviewed media.</summary>
    [Required(ErrorMessage = "MediaId is required")]
    [ForeignKey(nameof(Media))]
    public Guid MediaId { get; set; }
    
    /// <summary>Optional free-text comment.</summary>
    [StringLength(2000, ErrorMessage = "Comment cannot exceed 2000 characters")]
    public string? Comment { get; set; }
    
    /// <summary>Score from 1 to 10.</summary>
    [Required(ErrorMessage = "Rating is required")]
    [Range(1, 10, ErrorMessage = "Rating must be between 1 and 10")]
    public int Rating { get; set; }
    
    /// <summary>When the review was written (UTC).</summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Last edit (UTC); null if never edited.</summary>
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>Navigation to the author.</summary>
    public User? User { get; set; }

    /// <summary>Navigation to the reviewed media.</summary>
    public Media? Media { get; set; }
}
