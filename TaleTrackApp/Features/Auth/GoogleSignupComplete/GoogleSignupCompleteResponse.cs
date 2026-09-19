namespace TaleTrackApp.Features.Auth.GoogleSignupComplete;

public class GoogleSignupCompleteResponse
{
    public bool Success { get; set; }
    public required string Message { get; set; }
    public required string Token { get; set; }
    public required string RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
}
