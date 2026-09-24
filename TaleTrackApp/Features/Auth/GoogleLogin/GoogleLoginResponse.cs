namespace TaleTrackApp.Features.Auth.GoogleLogin;

/// <summary>
/// Covers both outcomes of a Google sign-in: a brand-new signup only fills
/// <see cref="NeedsUsername"/>/<see cref="PendingToken"/>/<see cref="Email"/>/<see cref="SuggestedUsername"/>;
/// an existing account only fills the token fields.
/// </summary>
public class GoogleLoginResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>True when the Google account is new and the sign-up must be completed with a username. Then only `pendingToken`, `email` and `suggestedUsername` are set.</summary>
    public bool NeedsUsername { get; set; }
    /// <summary>Short-lived token to send to `POST /api/auth/google/complete`. Only when `needsUsername` is true.</summary>
    public string? PendingToken { get; set; }
    /// <summary>Email of the Google account. Only when `needsUsername` is true.</summary>
    public string? Email { get; set; }
    /// <summary>A free username derived from the email, offered as a default. Only when `needsUsername` is true.</summary>
    public string? SuggestedUsername { get; set; }
    /// <summary>Confirmation in English, for an existing account.</summary>
    public string? Message { get; set; }
    /// <summary>Access token (JWT). Send it in the `Authorization` header as `Bearer {token}`. Only for an existing account.</summary>
    public string? Token { get; set; }
    /// <summary>Refresh token. Store it securely and exchange it with `POST /api/auth/refresh`. Only for an existing account.</summary>
    public string? RefreshToken { get; set; }
    /// <summary>Access token lifetime in seconds. Only for an existing account.</summary>
    public int? ExpiresIn { get; set; }
    /// <summary>True when an existing email-and-password account with the same email was just linked to this Google identity.</summary>
    public bool LinkedExistingAccount { get; set; }
}
