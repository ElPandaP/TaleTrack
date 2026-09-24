namespace TaleTrackApp.Features.Auth.Login;

public class LoginResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>Human-readable confirmation in English.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>Access token (JWT). Send it in the `Authorization` header as `Bearer {token}`.</summary>
    public string Token { get; set; } = string.Empty;
    /// <summary>Refresh token. Store it securely and exchange it with `POST /api/auth/refresh`.</summary>
    public string RefreshToken { get; set; } = string.Empty;
    /// <summary>Access token lifetime in seconds.</summary>
    public int ExpiresIn { get; set; }
}
