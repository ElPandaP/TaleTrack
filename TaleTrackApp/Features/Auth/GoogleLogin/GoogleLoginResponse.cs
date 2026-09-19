namespace TaleTrackApp.Features.Auth.GoogleLogin;

/// <summary>
/// Covers both outcomes of a Google sign-in: a brand-new signup only fills
/// <see cref="NeedsUsername"/>/<see cref="PendingToken"/>/<see cref="Email"/>/<see cref="SuggestedUsername"/>;
/// an existing account only fills the token fields.
/// </summary>
public class GoogleLoginResponse
{
    public bool Success { get; set; }
    public bool NeedsUsername { get; set; }
    public string? PendingToken { get; set; }
    public string? Email { get; set; }
    public string? SuggestedUsername { get; set; }
    public string? Message { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public int? ExpiresIn { get; set; }
    public bool LinkedExistingAccount { get; set; }
}
