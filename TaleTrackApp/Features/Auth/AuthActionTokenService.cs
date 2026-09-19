using Microsoft.EntityFrameworkCore;
using TaleTrackApp.Data;
using TaleTrackApp.Features.User;
using TaleTrackApp.Model;

namespace TaleTrackApp.Features.Auth;

/// <summary>
/// Issues and consumes the single-use tokens carried by email links
/// (password reset, delete confirmation, "I didn't sign up"). Raw tokens live
/// only in the emailed link; the DB stores their SHA-256 hash. Also owns sending those emails.
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
    private readonly UserService _users;
    private readonly EmailService _email;
    private readonly ILogger<AuthActionTokenService> _logger;

    public AuthActionTokenService(
        AppDbContext context,
        UserService users,
        EmailService email,
        ILogger<AuthActionTokenService> logger)
    {
        _context = context;
        _users = users;
        _email = email;
        _logger = logger;
    }

    /// <summary>Creates a token for the given purpose and returns the raw value (shown once).</summary>
    public async Task<string> IssueAsync(Guid userId, string purpose)
    {
        var raw = TokenHasher.GenerateRawToken();
        _context.AuthActionTokens.Add(new AuthActionToken
        {
            UserId = userId,
            TokenHash = TokenHasher.Hash(raw),
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

        var hash = TokenHasher.Hash(rawToken);
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

    /// <summary>Sent to every newly created account: a 7-day link that deletes the account, for
    /// when someone signed up with an address they don't own.</summary>
    public async Task SendWelcomeAsync(Guid userId, string email, string? locale)
    {
        var raw = await IssueAsync(userId, AuthActionToken.SignupRevoke);
        await _email.SendWelcomeAsync(email, raw, locale);
    }

    /// <summary>Emails a reset link if the account exists and has a password. Silent otherwise,
    /// so the caller's response can't reveal whether the address is registered.</summary>
    public async Task SendPasswordResetAsync(string email, string? locale)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash)) return;

        var raw = await IssueAsync(user.Id, AuthActionToken.PasswordReset);
        await _email.SendPasswordResetAsync(user.Email, raw, locale);
    }

    public async Task SendDeleteConfirmationAsync(Guid userId, string email, string? locale)
    {
        var raw = await IssueAsync(userId, AuthActionToken.DeleteAccount);
        await _email.SendDeleteConfirmationAsync(email, raw, locale);
    }
}
