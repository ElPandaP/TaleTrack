using System.Security.Cryptography;
using System.Text;
using TaleTrackApp.Data;
using TaleTrackApp.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace TaleTrackApp.Features.User;

/// <summary>Changes to the per-type feed-privacy flags. A null flag is left unchanged.</summary>
/// <param name="BookProgress">Share book progress with friends.</param>
/// <param name="BookReviews">Share book reviews with friends.</param>
/// <param name="MovieProgress">Share film progress with friends.</param>
/// <param name="MovieReviews">Share film reviews with friends.</param>
/// <param name="SeriesProgress">Share series progress with friends.</param>
/// <param name="SeriesReviews">Share series reviews with friends.</param>
public record FeedPrivacy(
    bool? BookProgress = null, bool? BookReviews = null,
    bool? MovieProgress = null, bool? MovieReviews = null,
    bool? SeriesProgress = null, bool? SeriesReviews = null);

/// <summary>Outcome of <see cref="UserService.RegisterAsync"/>.</summary>
public enum RegisterResult { Ok, EmailTaken, UsernameTaken }

/// <summary>Outcome of <see cref="UserService.UpdateUserAsync"/>.</summary>
public enum UpdateUserResult { Ok, NotFound, UsernameTaken }

/// <summary>
/// User accounts: lookups, registration, password and one-time email code checks, Google
/// account linking and profile updates. Used by the user endpoints and by the auth feature.
/// </summary>
public class UserService
{
    /// <summary>
    /// Password hasher: PBKDF2 (HMAC-SHA512) with a random salt per hash, via ASP.NET Core
    /// Identity. The iteration count follows the OWASP recommendation for PBKDF2-HMAC-SHA512.
    /// </summary>
    private static readonly PasswordHasher<Model.User> PasswordHasher =
        new(Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));

    /// <summary>Wrong guesses allowed against a one-time email code before it is thrown away.</summary>
    private const int MaxEmailCodeAttempts = 5;

    /// <summary>Database context used to read and write users.</summary>
    private readonly AppDbContext _context;
    /// <summary>Logger for this service.</summary>
    private readonly ILogger<UserService> _logger;

    /// <summary>Creates the service with its database context and logger.</summary>
    public UserService(AppDbContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>The user with this email, compared case-insensitively and ignoring surrounding spaces, or null.</summary>
    public async Task<Model.User?> GetByEmailAsync(string email)
    {
        var e = email.Trim().ToLower();
        return await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == e);
    }

    /// <summary>The user with this username, compared case-insensitively and ignoring surrounding spaces, or null.</summary>
    public async Task<Model.User?> GetByUsernameAsync(string username)
    {
        var u = username.Trim().ToLower();
        return await _context.Users.FirstOrDefaultAsync(x => x.Username.ToLower() == u);
    }

    /// <summary>The user with this id, or null.</summary>
    public async Task<Model.User?> GetByIdAsync(Guid id)
    {
        return await _context.Users.FindAsync(id);
    }

    /// <summary>Whether an account already uses this email (case-insensitive).</summary>
    private async Task<bool> EmailExistsAsync(string email)
    {
        var e = email.Trim().ToLower();
        return await _context.Users.AnyAsync(u => u.Email.ToLower() == e);
    }

    /// <summary>Whether an account already uses this username (case-insensitive).</summary>
    public async Task<bool> UsernameExistsAsync(string username)
    {
        var u = username.Trim().ToLower();
        return await _context.Users.AnyAsync(x => x.Username.ToLower() == u);
    }

    /// <summary>Creates a password account unless the email or the username is already taken.</summary>
    public async Task<(RegisterResult Result, Model.User? User)> RegisterAsync(
        string email, string username, string password)
    {
        if (await EmailExistsAsync(email))
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", email);
            return (RegisterResult.EmailTaken, null);
        }

        if (await UsernameExistsAsync(username))
        {
            _logger.LogWarning("Registration attempt with existing username: {Username}", username);
            return (RegisterResult.UsernameTaken, null);
        }

        return (RegisterResult.Ok, await CreateUserAsync(email, username, password));
    }

    /// <summary>The user with these credentials, or null if the email is unknown or the password is wrong.</summary>
    public async Task<Model.User?> AuthenticateAsync(string email, string password)
    {
        var user = await GetByEmailAsync(email);
        if (user == null || !VerifyPassword(password, user))
        {
            _logger.LogWarning("Failed login attempt for email: {Email}", email);
            return null;
        }

        return user;
    }

    /// <summary>Inserts a password account. The caller has already checked that email and username are free.</summary>
    private async Task<Model.User> CreateUserAsync(string email, string username, string password)
    {
        var user = new Model.User
        {
            Email = email,
            Username = username,
        };
        user.PasswordHash = HashPassword(user, password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Username} created", user.Username);
        return user;
    }

    /// <summary>True if a user other than <paramref name="userId"/> already has this username (case-insensitive).</summary>
    private async Task<bool> UsernameTakenByOtherAsync(Guid userId, string username)
    {
        var u = username.Trim().ToLower();
        return await _context.Users.AnyAsync(x => x.Id != userId && x.Username.ToLower() == u);
    }

    /// <summary>
    /// Applies a partial profile update: a new username when given and different, and any
    /// feed-privacy flags that are not null.
    /// </summary>
    /// <returns>The outcome and, when it is <see cref="UpdateUserResult.Ok"/>, the updated user.</returns>
    public async Task<(UpdateUserResult Result, Model.User? User)> UpdateUserAsync(
        Guid id,
        string? username,
        FeedPrivacy? privacy = null)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return (UpdateUserResult.NotFound, null);
        }

        var changesUsername = !string.IsNullOrEmpty(username) && username != user.Username;
        if (changesUsername)
        {
            if (await UsernameTakenByOtherAsync(id, username!))
            {
                _logger.LogWarning("User {UserId} tried to take an existing username: {Username}", id, username);
                return (UpdateUserResult.UsernameTaken, null);
            }

            user.Username = username!;
        }

        if (privacy != null)
        {
            if (privacy.BookProgress.HasValue) user.ShareBookProgress = privacy.BookProgress.Value;
            if (privacy.BookReviews.HasValue) user.ShareBookReviews = privacy.BookReviews.Value;
            if (privacy.MovieProgress.HasValue) user.ShareMovieProgress = privacy.MovieProgress.Value;
            if (privacy.MovieReviews.HasValue) user.ShareMovieReviews = privacy.MovieReviews.Value;
            if (privacy.SeriesProgress.HasValue) user.ShareSeriesProgress = privacy.SeriesProgress.Value;
            if (privacy.SeriesReviews.HasValue) user.ShareSeriesReviews = privacy.SeriesReviews.Value;
        }

        user.UpdatedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException) when (changesUsername)
        {
            // Someone may have taken the name between the check above and the write, which the
            // unique index catches. Any other write failure is not ours to swallow.
            _context.ChangeTracker.Clear();
            if (await UsernameTakenByOtherAsync(id, username!))
                return (UpdateUserResult.UsernameTaken, null);
            throw;
        }

        _logger.LogInformation("User {UserId} updated", id);
        return (UpdateUserResult.Ok, user);
    }

    /// <summary>
    /// Deletes an account. The database cascade removes everything that belongs to it.
    /// </summary>
    /// <returns>False if the user did not exist.</returns>
    public async Task<bool> DeleteUserAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return false;
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} deleted", id);
        return true;
    }

    /// <summary>The user linked to this Google account id, or null.</summary>
    public async Task<Model.User?> GetByGoogleIdAsync(string googleId)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId);
    }

    /// <summary>
    /// Creates an account that signs in with Google (no password). If the username is taken, a
    /// number is appended until it is free; it is cut to fit the 50-character limit.
    /// </summary>
    public async Task<Model.User> CreateGoogleUserAsync(string email, string username, string googleId)
    {
        var baseUsername = username.Length > 50 ? username[..50] : username;
        var finalUsername = baseUsername;
        int suffix = 1;
        while (await _context.Users.AnyAsync(u => u.Username == finalUsername))
        {
            var limit = Math.Min(baseUsername.Length, 46);
            finalUsername = $"{baseUsername[..limit]}{suffix++}";
        }

        var user = new Model.User
        {
            Email = email,
            Username = finalUsername,
            GoogleId = googleId
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Google user {Username} created", user.Username);
        return user;
    }

    /// <summary>Links a Google account id to an existing user. Does nothing if the user does not exist.</summary>
    public async Task LinkGoogleIdAsync(Guid id, string googleId)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return;

        user.GoogleId = googleId;
        user.UpdatedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Generates a one-time login code for the account with this email and stores it (valid for 10
    /// minutes). Returns null if there is no such account.
    /// </summary>
    public async Task<(Model.User User, string Code)?> IssueEmailCodeAsync(string email)
    {
        var user = await GetByEmailAsync(email);
        if (user == null) return null;

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        await SetEmailCodeAsync(user.Id, code);
        return (user, code);
    }

    /// <summary>
    /// Checks a one-time login code and consumes it. Returns the user, or null if the account has no
    /// pending code, the code has expired or it doesn't match.
    /// </summary>
    public async Task<Model.User?> VerifyEmailCodeAsync(string email, string code)
    {
        var user = await GetByEmailAsync(email);
        if (user == null || user.EmailCode == null || user.EmailCodeExpiry == null)
            return null;

        if (user.EmailCodeExpiry < DateTime.UtcNow)
        {
            _logger.LogWarning("Expired email code attempt for {Email}", email);
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(user.EmailCode), Encoding.UTF8.GetBytes(code)))
        {
            _logger.LogWarning("Invalid email code attempt for {Email}", email);
            await RegisterFailedEmailCodeAttemptAsync(user);
            return null;
        }

        await ClearEmailCodeAsync(user.Id);
        return user;
    }

    /// <summary>Stores a new one-time code for the user, valid for 10 minutes, and resets the failed-attempt count.</summary>
    private async Task SetEmailCodeAsync(Guid id, string code)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return;

        user.EmailCode = code;
        user.EmailCodeExpiry = DateTime.UtcNow.AddMinutes(10);
        user.EmailCodeFailedAttempts = 0;
        user.UpdatedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    /// <summary>Removes the user's one-time code once it has been used.</summary>
    private async Task ClearEmailCodeAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return;

        user.EmailCode = null;
        user.EmailCodeExpiry = null;
        user.EmailCodeFailedAttempts = 0;
        user.UpdatedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    /// <summary>Counts a wrong guess; after <see cref="MaxEmailCodeAttempts"/> the code is discarded and a new one must be requested.</summary>
    private async Task RegisterFailedEmailCodeAttemptAsync(Model.User user)
    {
        user.EmailCodeFailedAttempts++;
        if (user.EmailCodeFailedAttempts >= MaxEmailCodeAttempts)
        {
            _logger.LogWarning("Email code for {Email} discarded after too many failed attempts", user.Email);
            user.EmailCode = null;
            user.EmailCodeExpiry = null;
            user.EmailCodeFailedAttempts = 0;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    /// <summary>Sets a new password for a user, hashed with <see cref="PasswordHasher"/>. Returns false if the user is gone.</summary>
    public async Task<bool> SetPasswordAsync(Guid userId, string newPassword)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.PasswordHash = HashPassword(user, newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Password reset for user {UserId}", userId);
        return true;
    }

    /// <summary>Whether <paramref name="password"/> matches the user's stored hash. Always false for accounts without a password.</summary>
    private bool VerifyPassword(string password, Model.User user)
    {
        if (string.IsNullOrEmpty(user.PasswordHash)) return false;
        var result = PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result != PasswordVerificationResult.Failed;
    }

    /// <summary>Hashes a password for storage on the user.</summary>
    private static string HashPassword(Model.User user, string password)
        => PasswordHasher.HashPassword(user, password);
}

