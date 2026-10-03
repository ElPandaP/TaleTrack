namespace TaleTrackApp.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// A device's session: the long-lived credential it holds to mint fresh access tokens.
/// The raw token is never stored, only its SHA-256 hash. One row per device/session,
/// so the user can revoke them individually from the "Conexiones" page; each refresh
/// rotates the token in place (new hash, same row and id).
/// </summary>
public class RefreshToken
{
    /// <summary>Primary key; also the session id shown and revoked from the "Conexiones" page.</summary>
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Account the session belongs to.</summary>
    [Required]
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }

    /// <summary>Hex-encoded SHA-256 of the raw token.</summary>
    [Required]
    [StringLength(64)]
    public required string TokenHash { get; set; }

    /// <summary>Human label for the "Conexiones" list, e.g. "Netflix extension", "Web", "KOReader".</summary>
    [Required]
    [StringLength(60)]
    public string Device { get; set; } = "Unknown";

    /// <summary>When the session was started (UTC).</summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Last time the token was issued or rotated (UTC).</summary>
    [Required]
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the token stops being accepted (UTC). Each rotation pushes it forward again.</summary>
    [Required]
    public DateTime ExpiresAt { get; set; }

    /// <summary>When the session was ended (logout, revocation or password reset); null while active.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Navigation to the owning account.</summary>
    public User? User { get; set; }
}
