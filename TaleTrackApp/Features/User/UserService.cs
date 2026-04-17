using TaleTrackApp.Data;
using TaleTrackApp.Model;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace TaleTrackApp.Features.User;

public class UserService
{
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
            PasswordHash = HashPassword(password)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"User {user.Username} created successfully");
        return user;
    }

    public async Task<Model.User?> UpdateUserAsync(int id, string? username, string? email, string? password)
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
            user.PasswordHash = HashPassword(password);
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

    public bool VerifyPassword(string password, string? hash)
    {
        if (hash == null) return false;
        return HashPassword(password) == hash;
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
}

