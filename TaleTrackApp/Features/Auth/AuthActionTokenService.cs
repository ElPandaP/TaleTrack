using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TaleTrackApp.Data;
using TaleTrackApp.Model;

namespace TaleTrackApp.Features.Auth;

/// <summary>
/// Issues and consumes the single-use tokens carried by email links
/// (password reset, delete confirmation, "I didn't sign up"). Raw tokens live
/// only in the emailed link; the DB stores their SHA-256 hash.
/// </summary>
public class AuthActionTokenService
{
    private static readonly Dictionary<string, TimeSpan> Lifetimes = new()
    {
        [AuthActionToken.PasswordReset] = TimeSpan.FromHours(1),
        [AuthActionToken.DeleteAccount] = TimeSpan.FromHours(1),
        [AuthActionToken.SignupRevoke] = TimeSpan.FromDays(7),
    };

    private readonly AppDbContext _context;
    private readonly ILogger<AuthActionTokenService> _logger;

    public AuthActionTokenService(AppDbContext context, ILogger<AuthActionTokenService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Creates a token for the given purpose and returns the raw value (shown once).</summary>
    public async Task<string> IssueAsync(int userId, string purpose)
    {
        var raw = GenerateRawToken();
        _context.AuthActionTokens.Add(new AuthActionToken
        {
            UserId = userId,
            TokenHash = Hash(raw),
            Purpose = purpose,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(Lifetimes[purpose]),
        });
        await _context.SaveChangesAsync();
        return raw;
    }

    /// <summary>
    /// Validates a raw token against a purpose and marks it consumed. Returns the owning
    /// user, or null if the token is unknown, the wrong purpose, expired or already used.
    /// </summary>
    public async Task<Model.User?> ConsumeAsync(string rawToken, string purpose)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;

        var hash = Hash(rawToken);
        var token = await _context.AuthActionTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose);

        if (token?.User is null)
            return null;

        if (token.ConsumedAt != null || token.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Rejected {Purpose} token for user {UserId} (consumed or expired)",
                purpose, token.UserId);
            return null;
        }

        token.ConsumedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return token.User;
    }
}
