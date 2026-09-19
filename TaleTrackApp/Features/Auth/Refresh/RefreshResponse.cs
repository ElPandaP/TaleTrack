namespace TaleTrackApp.Features.Auth.Refresh;

public class RefreshResponse
{
    public bool Success { get; set; }
    public required string Token { get; set; }
    public required string RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
}
