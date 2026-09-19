using System.Security.Cryptography;
using System.Text;

namespace TaleTrackApp.Features.Auth;

/// <summary>Opaque random tokens shared by refresh tokens and email-link tokens: the raw value
/// travels to the client, only its SHA-256 hash is stored.</summary>
public static class TokenHasher
{
    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
