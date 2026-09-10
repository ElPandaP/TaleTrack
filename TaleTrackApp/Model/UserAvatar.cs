namespace TaleTrackApp.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// A user's uploaded profile photo, already resized to a square. Kept in its own
/// table so ordinary <see cref="User"/> queries never pull the image bytes.
/// </summary>
public class UserAvatar
{
    [Key]
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }

    [Required]
    public required byte[] Data { get; set; }

    [Required]
    [StringLength(50)]
    public required string ContentType { get; set; }

    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
