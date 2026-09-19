using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TaleTrackApp.Data;
using TaleTrackApp.Model;
using TaleTrackApp.Services;

namespace TaleTrackApp.Features.Auth;

/// <summary>What every login flow hands back to the client. ExpiresIn is the access token lifetime in seconds.</summary>
public record SessionTokens(string AccessToken, string RefreshToken, int ExpiresIn);

/// <summary>A login session: an access JWT plus a rotating refresh token, one per device. Starts,
/// refreshes, lists and revokes them. Raw refresh tokens live only in the client;
/// the DB stores their SHA-256 hash.</summary>
public class SessionService
{
    /// <summary>How long a just-rotated token keeps returning the same replacement, so
    /// concurrent refreshers (middleware + client, multiple tabs) don't knock each other out.</summary>
    private static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(60);

    private readonly AppDbContext _context;
    private readonly JwtService _jwt;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        AppDbContext context,
        JwtService jwt,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<SessionService> logger)
    {
        _context = context;
        _jwt = jwt;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    private int RefreshTokenDays =>
        int.TryParse(_configuration["JwtSettings:RefreshTokenDays"], out var d) ? d : 60;

    private static string GraceKey(string tokenHash) => $"rt-rotated:{tokenHash}";

    /// <summary>Logs the user in on a device: a fresh access token and refresh token.</summary>
    public async Task<SessionTokens> StartAsync(Model.User user, string device) =>
        new(_jwt.GenerateToken(user.Id, user.Email, user.Username),
            await IssueRefreshTokenAsync(user.Id, device),
            _jwt.ExpirationMinutes * 60);

    /// <summary>Exchanges a refresh token for a new pair. Null if it is unknown, revoked or expired.</summary>
    public async Task<SessionTokens?> RefreshAsync(string rawToken)
    {
        var rotated = await ValidateAndRotateAsync(rawToken);
        if (rotated is null) return null;

        var (user, newRawToken) = rotated.Value;
        _logger.LogInformation("Refreshed tokens for user {UserId}", user.Id);
        return new(_jwt.GenerateToken(user.Id, user.Email, user.Username), newRawToken, _jwt.ExpirationMinutes * 60);
    }

    /// <summary>Creates a new refresh token for a device and returns the raw value (shown once).</summary>
    private async Task<string> IssueRefreshTokenAsync(Guid userId, string device)
    {
        var raw = TokenHasher.GenerateRawToken();
        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = TokenHasher.Hash(raw),
            Device = string.IsNullOrWhiteSpace(device) ? "Unknown" : device.Trim(),
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenDays),
        });
        await _context.SaveChangesAsync();
        return raw;
    }

    /// <summary>
    /// Validates a raw refresh token and rotates it (single-use): the presented token is
    /// revoked and a fresh one is issued for the same device with a slid expiry.
    /// Returns null if the token is unknown, revoked or expired.
    /// </summary>
    private async Task<(Model.User User, string NewRawToken)?> ValidateAndRotateAsync(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;

        var hash = TokenHasher.Hash(rawToken);

        // A token rotated moments ago replays its replacement instead of failing —
        // but only while that replacement is itself still active (not since revoked).
        if (_cache.TryGetValue(GraceKey(hash), out (Guid UserId, string NewRaw) grace))
        {
            var graceHash = TokenHasher.Hash(grace.NewRaw);
            var replacementLive = await _context.RefreshTokens.AnyAsync(rt =>
                rt.TokenHash == graceHash && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow);
            if (replacementLive)
            {
                var graceUser = await _context.Users.FindAsync(grace.UserId);
                if (graceUser != null) return (graceUser, grace.NewRaw);
            }
        }

        var existing = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash);

        if (existing?.User is null)
            return null;

        if (existing.RevokedAt != null || existing.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Rejected refresh token for user {UserId} (revoked or expired)", existing.UserId);
            return null;
        }

        var newRaw = TokenHasher.GenerateRawToken();
        existing.RevokedAt = DateTime.UtcNow;
        existing.ReplacedByTokenHash = TokenHasher.Hash(newRaw);

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = existing.UserId,
            TokenHash = existing.ReplacedByTokenHash,
            Device = existing.Device,
            CreatedAt = existing.CreatedAt,
            LastUsedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenDays),
        });
        await _context.SaveChangesAsync();

        _cache.Set(GraceKey(hash), (existing.UserId, newRaw), RotationGrace);

        return (existing.User, newRaw);
    }

    public async Task<List<RefreshToken>> ListActiveAsync(Guid userId) =>
        await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(rt => rt.LastUsedAt)
            .ToListAsync();

    /// <summary>Revokes every active session for a user (e.g. after a password reset).</summary>
    public async Task RevokeAllForUserAsync(Guid userId)
    {
        var active = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync();
        if (active.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var rt in active) rt.RevokedAt = now;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Revoked {Count} session(s) for user {UserId}", active.Count, userId);
    }

    /// <summary>Revokes whichever token matches this raw value, if any. Silent no-op otherwise.</summary>
    public async Task RevokeByRawTokenAsync(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return;
        var hash = TokenHasher.Hash(rawToken);
        var token = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == hash);
        if (token is { RevokedAt: null })
        {
            token.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>Revokes one session by id. Returns false if it does not belong to the user.</summary>
    public async Task<bool> RevokeAsync(Guid userId, Guid id)
    {
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Id == id && rt.UserId == userId);
        if (token is null) return false;

        if (token.RevokedAt == null)
        {
            token.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        return true;
    }
}
