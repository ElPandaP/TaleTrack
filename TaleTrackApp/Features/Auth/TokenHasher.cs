using System.Security.Cryptography;
using System.Text;

namespace TaleTrackApp.Features.Auth;

/// <summary>Opaque random tokens shared by refresh tokens and email-link tokens: the raw value
/// travels to the client, only its SHA-256 hash is stored.</summary>
public static class TokenHasher
{
    /// <summary>SHA-256 of the raw token as lowercase hex: the value stored in the database.</summary>
    /// <param name="rawToken">The token as the client holds it.</param>
    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>A new random token: 32 bytes from a cryptographic generator, base64url-encoded.</summary>
    public static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
