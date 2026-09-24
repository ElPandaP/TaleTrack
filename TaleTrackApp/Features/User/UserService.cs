using System.Security.Cryptography;
using System.Text;
using TaleTrackApp.Data;
using TaleTrackApp.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace TaleTrackApp.Features.User;

/// <summary>Per-media-type feed privacy patch. Null = leave unchanged.</summary>
public record FeedPrivacy(
    bool? BookProgress = null, bool? BookReviews = null,
    bool? MovieProgress = null, bool? MovieReviews = null,
    bool? SeriesProgress = null, bool? SeriesReviews = null);

/// <summary>Outcome of <see cref="UserService.RegisterAsync"/>.</summary>
public enum RegisterResult { Ok, EmailTaken, UsernameTaken }

/// <summary>Outcome of <see cref="UserService.UpdateUserAsync"/>.</summary>
public enum UpdateUserResult { Ok, NotFound, UsernameTaken }

public class UserService
{
    // PBKDF2 (HMAC-SHA512) with a per-hash random salt, via ASP.NET Core Identity's hasher.
    // Iteration count set to the OWASP Password Storage recommendation for PBKDF2-HMAC-SHA512.
    private static readonly PasswordHasher<Model.User> PasswordHasher =
        new(Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));

    // Wrong guesses allowed against a one-time email code before it is thrown away.
    private const int MaxEmailCodeAttempts = 5;

    private readonly AppDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(AppDbContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Model.User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<Model.User?> GetByUsernameAsync(string username)
    {
        var u = username.Trim().ToLower();
        return await _context.Users.FirstOrDefaultAsync(x => x.Username.ToLower() == u);
    }

    public async Task<Model.User?> GetByIdAsync(Guid id)
    {
        return await _context.Users.FindAsync(id);
    }

    private async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email == email);
    }

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

        _logger.LogInformation($"User {user.Username} created successfully");
        return user;
    }

    /// <summary>True if a user other than <paramref name="userId"/> already has this username (case-insensitive).</summary>
    private async Task<bool> UsernameTakenByOtherAsync(Guid userId, string username)
    {
        var u = username.Trim().ToLower();
        return await _context.Users.AnyAsync(x => x.Id != userId && x.Username.ToLower() == u);
    }

    /// <summary>Applies a partial profile update. Unless the username is taken, the user comes back as well.</summary>
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

        _logger.LogInformation($"User {id} updated successfully");
        return (UpdateUserResult.Ok, user);
    }

    public async Task<bool> DeleteUserAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return false;
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"User {id} deleted successfully");
        return true;
    }

    public async Task<Model.User?> GetByGoogleIdAsync(string googleId)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId);
    }

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

    /// <summary>Sets a new password (PBKDF2) for a user. Returns false if the user is gone.</summary>
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

    private bool VerifyPassword(string password, Model.User user)
    {
        if (string.IsNullOrEmpty(user.PasswordHash)) return false;
        var result = PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result != PasswordVerificationResult.Failed;
    }

    private static string HashPassword(Model.User user, string password)
        => PasswordHasher.HashPassword(user, password);
}

