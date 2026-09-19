namespace TaleTrackApp.Features.Auth.VerifyCode;

public class VerifyCodeResponse
{
    public bool Success { get; set; }
    public required string Message { get; set; }
    public required string Token { get; set; }
    public required string RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
}
