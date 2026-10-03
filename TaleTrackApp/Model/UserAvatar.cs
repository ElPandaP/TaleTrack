namespace TaleTrackApp.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// A user's uploaded profile photo, already resized to a square. Kept in its own
/// table so ordinary <see cref="User"/> queries never pull the image bytes.
/// </summary>
public class UserAvatar
{
    /// <summary>Owner of the photo; also the primary key, so a user has at most one avatar.</summary>
    [Key]
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }

    /// <summary>Encoded image bytes.</summary>
    [Required]
    public required byte[] Data { get; set; }

    /// <summary>MIME type of <see cref="Data"/>, sent as the response content type.</summary>
    [Required]
    [StringLength(50)]
    public required string ContentType { get; set; }

    /// <summary>When the photo was last uploaded (UTC).</summary>
    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the owner.</summary>
    public User? User { get; set; }
}
