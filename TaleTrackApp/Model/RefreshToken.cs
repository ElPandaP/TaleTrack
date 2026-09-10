namespace TaleTrackApp.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// A long-lived credential a single device/client holds to mint fresh access tokens.
/// The raw token is never stored — only its SHA-256 hash. One row per device/session,
/// so the user can revoke them individually from the "Conexiones" page.
/// </summary>
public class RefreshToken
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }

    /// <summary>Hex-encoded SHA-256 of the raw token.</summary>
    [Required]
    [StringLength(64)]
    public required string TokenHash { get; set; }

    /// <summary>Human label for the "Conexiones" list, e.g. "Netflix extension", "Web", "KOReader".</summary>
    [Required]
    [StringLength(60)]
    public string Device { get; set; } = "Unknown";

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>Hash of the token this one was rotated into (rotation audit trail).</summary>
    [StringLength(64)]
    public string? ReplacedByTokenHash { get; set; }

    public User? User { get; set; }

    [NotMapped]
    public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;
}
