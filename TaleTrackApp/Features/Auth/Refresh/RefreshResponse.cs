namespace TaleTrackApp.Features.Auth.Refresh;

public class RefreshResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>Access token (JWT). Send it in the `Authorization` header as `Bearer {token}`.</summary>
    public required string Token { get; set; }
    /// <summary>The new refresh token. It replaces the one that was sent, which stops being valid.</summary>
    public required string RefreshToken { get; set; }
    /// <summary>Access token lifetime in seconds.</summary>
    public int ExpiresIn { get; set; }
}
