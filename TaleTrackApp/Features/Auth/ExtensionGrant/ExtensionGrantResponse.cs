namespace TaleTrackApp.Features.Auth.ExtensionGrant;

public class ExtensionGrantResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>Access token (JWT). Send it in the `Authorization` header as `Bearer {token}`.</summary>
    public required string Token { get; set; }
    /// <summary>Refresh token. Store it securely and exchange it with `POST /api/auth/refresh`.</summary>
    public required string RefreshToken { get; set; }
    /// <summary>Access token lifetime in seconds.</summary>
    public int ExpiresIn { get; set; }
}
