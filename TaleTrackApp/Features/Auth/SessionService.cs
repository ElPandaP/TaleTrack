using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TaleTrackApp.Data;
using TaleTrackApp.Model;
using TaleTrackApp.Services;

namespace TaleTrackApp.Features.Auth;

/// <summary>What every login flow hands back to the client.</summary>
/// <param name="AccessToken">Short-lived JWT sent as the bearer token.</param>
/// <param name="RefreshToken">Raw refresh token that renews the session.</param>
/// <param name="ExpiresIn">Access token lifetime in seconds.</param>
public record SessionTokens(string AccessToken, string RefreshToken, int ExpiresIn);

/// <summary>A login session: an access JWT plus a rotating refresh token, one per device. Starts,
/// refreshes, lists and revokes them. Raw refresh tokens live only in the client;
/// the DB stores their SHA-256 hash.</summary>
public class SessionService
{
    /// <summary>How long a just-rotated token keeps returning the same replacement, so
    /// concurrent refreshers (middleware + client, multiple tabs) don't knock each other out.</summary>
    private static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(60);

    /// <summary>Serializes rotations across requests (see <see cref="ValidateAndRotateAsync"/>).</summary>
    private static readonly SemaphoreSlim RotationLock = new(1, 1);

    /// <summary>Database context.</summary>
    private readonly AppDbContext _context;
    /// <summary>Issues the access tokens.</summary>
    private readonly JwtService _jwt;
    /// <summary>Application configuration (the <c>JwtSettings</c> section).</summary>
    private readonly IConfiguration _configuration;
    /// <summary>In-memory cache holding the grace entries of rotated tokens.</summary>
    private readonly IMemoryCache _cache;
    /// <summary>Logger.</summary>
    private readonly ILogger<SessionService> _logger;

    /// <summary>Creates the service with its dependencies (resolved by DI).</summary>
    /// <param name="context">Database context.</param>
    /// <param name="jwt">Issues the access tokens.</param>
    /// <param name="configuration">Source of <c>JwtSettings:RefreshTokenDays</c>.</param>
    /// <param name="cache">Holds the replacement of each just-rotated token during <see cref="RotationGrace"/>.</param>
    /// <param name="logger">Logger.</param>
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

    /// <summary>Refresh token lifetime in days (<c>JwtSettings:RefreshTokenDays</c>, 60 by default).</summary>
    private int RefreshTokenDays =>
        int.TryParse(_configuration["JwtSettings:RefreshTokenDays"], out var d) ? d : 60;

    /// <summary>Cache key under which a rotated token's replacement is kept.</summary>
    private static string GraceKey(string tokenHash) => $"rt-rotated:{tokenHash}";

    /// <summary>Logs the user in on a device: a fresh access token and refresh token.</summary>
    /// <param name="user">The authenticated user.</param>
    /// <param name="device">Label shown in the sessions list (e.g. "Web", "KOReader").</param>
    public async Task<SessionTokens> StartAsync(Model.User user, string device) =>
        new(_jwt.GenerateToken(user.Id, user.Email, user.Username),
            await IssueRefreshTokenAsync(user.Id, device),
            _jwt.ExpirationMinutes * 60);

    /// <summary>Exchanges a refresh token for a new pair. Null if it is unknown, revoked or expired.</summary>
    /// <param name="rawToken">The refresh token the client holds.</param>
    public async Task<SessionTokens?> RefreshAsync(string rawToken)
    {
        var rotated = await ValidateAndRotateAsync(rawToken);
        if (rotated is null) return null;

        var (user, newRawToken) = rotated.Value;
        _logger.LogInformation("Refreshed tokens for user {UserId}", user.Id);
        return new(_jwt.GenerateToken(user.Id, user.Email, user.Username), newRawToken, _jwt.ExpirationMinutes * 60);
    }

    /// <summary>Creates a new refresh token for a device and returns the raw value (shown once).</summary>
    /// <param name="userId">Owner of the session.</param>
    /// <param name="device">Session label; blank becomes "Unknown".</param>
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
    /// Validates a raw refresh token and rotates it (single-use) in place: the session's row
    /// gets a new token hash and a slid expiry, so the presented token stops working while
    /// the session keeps its id. Returns null if the token is unknown, revoked or expired.
    /// </summary>
    /// <param name="rawToken">The refresh token the client holds.</param>
    private async Task<(Model.User User, string NewRawToken)?> ValidateAndRotateAsync(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;

        var hash = TokenHasher.Hash(rawToken);

        // One rotation at a time: two concurrent refreshes with the same token would both
        // overwrite the row's hash and one caller would walk away with a dead token. Serialized,
        // the second one finds the grace entry the first just left.
        await RotationLock.WaitAsync();
        try
        {
            // A token rotated moments ago replays its replacement instead of failing, but only
            // while that replacement is itself still active (not since revoked).
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

            var session = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.TokenHash == hash);

            if (session?.User is null)
                return null;

            if (session.RevokedAt != null || session.ExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("Rejected refresh token for user {UserId} (revoked or expired)", session.UserId);
                return null;
            }

            var newRaw = TokenHasher.GenerateRawToken();
            session.TokenHash = TokenHasher.Hash(newRaw);
            session.LastUsedAt = DateTime.UtcNow;
            session.ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenDays);
            await _context.SaveChangesAsync();

            _cache.Set(GraceKey(hash), (session.UserId, newRaw), RotationGrace);

            return (session.User, newRaw);
        }
        finally
        {
            RotationLock.Release();
        }
    }

    /// <summary>The user's active sessions (not revoked, not expired), most recently used first.</summary>
    /// <param name="userId">Owner of the sessions.</param>
    public async Task<List<RefreshToken>> ListActiveAsync(Guid userId) =>
        await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(rt => rt.LastUsedAt)
            .ToListAsync();

    /// <summary>Revokes every active session for a user (e.g. after a password reset).</summary>
    /// <param name="userId">Owner of the sessions.</param>
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
    /// <param name="rawToken">The refresh token of the session to end.</param>
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

    /// <summary>Revokes one session by id. Returns false if it does not exist or does not belong to the user.</summary>
    /// <param name="userId">The caller, who must own the session.</param>
    /// <param name="id">Id of the session (<see cref="RefreshToken.Id"/>).</param>
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
