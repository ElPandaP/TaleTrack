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

public class UserService
{
    // PBKDF2 (HMAC-SHA512) with a per-hash random salt, via ASP.NET Core Identity's hasher.
    // Iteration count set to the OWASP Password Storage recommendation for PBKDF2-HMAC-SHA512.
    private static readonly PasswordHasher<Model.User> PasswordHasher =
        new(Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));

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

    public async Task<Model.User?> GetByIdAsync(int id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email == email);
    }

    public async Task<Model.User> CreateUserAsync(string email, string username, string password)
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

    public async Task<Model.User?> UpdateUserAsync(
        int id,
        string? username,
        string? email,
        string? password,
        string? avatarUrl = null,
        FeedPrivacy? privacy = null)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(username))
        {
            user.Username = username;
        }

        if (!string.IsNullOrEmpty(email))
        {
            user.Email = email;
        }

        if (!string.IsNullOrEmpty(password))
        {
            user.PasswordHash = HashPassword(user, password);
        }

        // Empty string clears the avatar; null leaves it unchanged.
        if (avatarUrl != null)
        {
            user.AvatarUrl = avatarUrl.Length == 0 ? null : avatarUrl;
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
        await _context.SaveChangesAsync();

        _logger.LogInformation($"User {id} updated successfully");
        return user;
    }

    public async Task<bool> DeleteUserAsync(int id)
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

    public async Task LinkGoogleIdAsync(int id, string googleId)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return;

        user.GoogleId = googleId;
        user.UpdatedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task SetEmailCodeAsync(int id, string code)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return;

        user.EmailCode = code;
        user.EmailCodeExpiry = DateTime.UtcNow.AddMinutes(10);
        user.UpdatedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task ClearEmailCodeAsync(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return;

        user.EmailCode = null;
        user.EmailCodeExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public bool VerifyPassword(string password, Model.User user)
    {
        if (string.IsNullOrEmpty(user.PasswordHash)) return false;
        var result = PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result != PasswordVerificationResult.Failed;
    }

    private static string HashPassword(Model.User user, string password)
        => PasswordHasher.HashPassword(user, password);
}

