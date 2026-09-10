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
    public const string PasswordReset = "password_reset";
    public const string DeleteAccount = "delete_account";
    public const string SignupRevoke = "signup_revoke";

    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }

    /// <summary>Hex-encoded SHA-256 of the raw token.</summary>
    [Required]
    [StringLength(64)]
    public required string TokenHash { get; set; }

    [Required]
    [StringLength(20)]
    public required string Purpose { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public User? User { get; set; }
}
