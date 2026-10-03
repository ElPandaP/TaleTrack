namespace TaleTrackApp.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// A single-use token delivered by email link: password reset, account-deletion
/// confirmation, or "I didn't sign up" revocation. The raw token lives only in the
/// emailed link; the DB stores its SHA-256 hash. One row per issued link.
/// </summary>
public class AuthActionToken
{
    /// <summary><see cref="Purpose"/> of a password-reset link.</summary>
    public const string PasswordReset = "password_reset";

    /// <summary><see cref="Purpose"/> of an account-deletion confirmation link.</summary>
    public const string DeleteAccount = "delete_account";

    /// <summary><see cref="Purpose"/> of the "I didn't sign up" link sent in the welcome email.</summary>
    public const string SignupRevoke = "signup_revoke";

    /// <summary>Primary key.</summary>
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Account the link acts on.</summary>
    [Required]
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }

    /// <summary>Hex-encoded SHA-256 of the raw token.</summary>
    [Required]
    [StringLength(64)]
    public required string TokenHash { get; set; }

    /// <summary>What the token is for: <see cref="PasswordReset"/>, <see cref="DeleteAccount"/> or <see cref="SignupRevoke"/>. A token only works for its own purpose.</summary>
    [Required]
    [StringLength(20)]
    public required string Purpose { get; set; }

    /// <summary>When the token was issued (UTC).</summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the token stops being accepted (UTC).</summary>
    [Required]
    public DateTime ExpiresAt { get; set; }

    /// <summary>When the token was used (UTC); null while it is still unused. A consumed token is never accepted again.</summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>Navigation to the owning account.</summary>
    public User? User { get; set; }
}
