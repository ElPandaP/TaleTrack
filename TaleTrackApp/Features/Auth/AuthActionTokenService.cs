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
    /// <summary>How long a token stays valid, per <see cref="AuthActionToken.Purpose"/>.</summary>
    private static readonly Dictionary<string, TimeSpan> Lifetimes = new()
    {
        [AuthActionToken.PasswordReset] = TimeSpan.FromHours(1),
        [AuthActionToken.DeleteAccount] = TimeSpan.FromHours(1),
        [AuthActionToken.SignupRevoke] = TimeSpan.FromDays(7),
    };

    /// <summary>Database context.</summary>
    private readonly AppDbContext _context;
    /// <summary>Account lookups and changes.</summary>
    private readonly UserService _users;
    /// <summary>Sends the emails.</summary>
    private readonly EmailService _email;
    /// <summary>Login sessions.</summary>
    private readonly SessionService _sessions;
    /// <summary>Logger.</summary>
    private readonly ILogger<AuthActionTokenService> _logger;

    /// <summary>Creates the service with its dependencies (resolved by DI).</summary>
    /// <param name="context">Database context.</param>
    /// <param name="users">Account lookups, password changes.</param>
    /// <param name="email">Sends the emails that carry the links.</param>
    /// <param name="sessions">Ends the user's sessions after a password reset.</param>
    /// <param name="logger">Logger.</param>
    public AuthActionTokenService(
        AppDbContext context,
        UserService users,
        EmailService email,
        SessionService sessions,
        ILogger<AuthActionTokenService> logger)
    {
        _context = context;
        _users = users;
        _email = email;
        _sessions = sessions;
        _logger = logger;
    }

    /// <summary>Creates a token for the given purpose and returns the raw value (shown once).</summary>
    /// <param name="userId">Account the token acts on.</param>
    /// <param name="purpose">One of the purpose constants of <see cref="AuthActionToken"/>; it sets the lifetime.</param>
    /// <returns>The raw token, to be put in the emailed link. Only its hash is stored.</returns>
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
    /// <param name="rawToken">Token taken from the emailed link.</param>
    /// <param name="purpose">Purpose the caller expects the token to have.</param>
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

    /// <summary>
    /// Sets a new password from a password-reset token and ends all of the user's sessions.
    /// Returns false if the token is unknown, expired or already used.
    /// </summary>
    /// <param name="rawToken">Token taken from the reset link.</param>
    /// <param name="newPassword">The password to set.</param>
    public async Task<bool> ResetPasswordAsync(string rawToken, string newPassword)
    {
        var user = await ConsumeAsync(rawToken, AuthActionToken.PasswordReset);
        if (user is null) return false;

        await _users.SetPasswordAsync(user.Id, newPassword);
        await _sessions.RevokeAllForUserAsync(user.Id);

        _logger.LogInformation("Password reset completed for user {UserId}", user.Id);
        return true;
    }

    /// <summary>Sends the welcome email of a new account. It includes a link, valid for 7 days, that
    /// deletes the account, for when someone signed up with an address they don't own.</summary>
    /// <param name="userId">The new account.</param>
    /// <param name="email">Address to send the email to.</param>
    /// <param name="locale">Language of the email ("es" or "en").</param>
    public async Task SendWelcomeAsync(Guid userId, string email, string? locale)
    {
        var raw = await IssueAsync(userId, AuthActionToken.SignupRevoke);
        await _email.SendWelcomeAsync(email, raw, locale);
    }

    /// <summary>Emails a reset link if the account exists and has a password. Silent otherwise,
    /// so the caller's response can't reveal whether the address is registered.</summary>
    /// <param name="email">Address the reset was requested for.</param>
    /// <param name="locale">Language of the email ("es" or "en").</param>
    public async Task SendPasswordResetAsync(string email, string? locale)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash)) return;

        var raw = await IssueAsync(user.Id, AuthActionToken.PasswordReset);
        await _email.SendPasswordResetAsync(user.Email, raw, locale);
    }

    /// <summary>Emails the link, valid for 1 hour, that confirms the deletion of the account.</summary>
    /// <param name="userId">Account to delete.</param>
    /// <param name="email">Address to send the email to.</param>
    /// <param name="locale">Language of the email ("es" or "en").</param>
    public async Task SendDeleteConfirmationAsync(Guid userId, string email, string? locale)
    {
        var raw = await IssueAsync(userId, AuthActionToken.DeleteAccount);
        await _email.SendDeleteConfirmationAsync(email, raw, locale);
    }
}
