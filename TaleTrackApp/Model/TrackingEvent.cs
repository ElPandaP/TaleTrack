namespace TaleTrackApp.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// A user's progress on a media. There is one row per user and media, updated in place as the
/// user advances; for a series it holds the latest season and episode reported.
/// </summary>
public class TrackingEvent
{
    /// <summary>Primary key.</summary>
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>User whose progress this is.</summary>
    [Required(ErrorMessage = "UserId is required")]
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }

    /// <summary>Tracked media.</summary>
    [Required(ErrorMessage = "MediaId is required")]
    [ForeignKey(nameof(Media))]
    public Guid MediaId { get; set; }
    
    /// <summary>Completion percentage (0 to 100); null while unknown.</summary>
    [Range(0, 100, ErrorMessage = "Progress must be between 0 and 100")]
    public int? Progress { get; set; }

    /// <summary>Latest season reported. Series only; null for movies and books.</summary>
    [Range(0, int.MaxValue)]
    public int? Season { get; set; }

    /// <summary>Latest episode reported within <see cref="Season"/>. Series only; null for movies and books.</summary>
    [Range(0, int.MaxValue)]
    public int? Episode { get; set; }

    /// <summary>When the progress was last recorded (UTC).</summary>
    [Required]
    public DateTime EventDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>Navigation to the user.</summary>
    public User? User { get; set; }

    /// <summary>Navigation to the tracked media.</summary>
    public Media? Media { get; set; }
}
