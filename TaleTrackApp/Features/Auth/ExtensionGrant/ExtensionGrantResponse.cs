namespace TaleTrackApp.Features.Auth.ExtensionGrant;

public class ExtensionGrantResponse
{
    public bool Success { get; set; }
    public required string Token { get; set; }
    public required string RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
}
