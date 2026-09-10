using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using TaleTrackApp.Data;
using TaleTrackApp.Model;

namespace TaleTrackApp.Features.User.Avatar;

/// <summary>Thrown when an uploaded file isn't a decodable image.</summary>
public class InvalidImageException(string message) : Exception(message);

public class AvatarService
{
    private const int Size = 256;
    private const string ContentType = "image/webp";

    private readonly AppDbContext _context;
    private readonly ILogger<AvatarService> _logger;

    public AvatarService(AppDbContext context, ILogger<AvatarService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<UserAvatar?> GetAsync(int userId) =>
        _context.UserAvatars.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId);

    /// <summary>
    /// Decodes, centre-crops to a <see cref="Size"/>×<see cref="Size"/> square, re-encodes as
    /// WebP, stores it and points the user's <c>AvatarUrl</c> at it. Returns the new URL.
    /// </summary>
    public async Task<string> SaveAsync(int userId, Stream imageStream)
    {
        byte[] webp;
        try
        {
            using var image = await Image.LoadAsync(imageStream);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(Size, Size),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center,
            }));

            using var ms = new MemoryStream();
            await image.SaveAsWebpAsync(ms, new WebpEncoder { Quality = 82 });
            webp = ms.ToArray();
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            throw new InvalidImageException("The file is not a supported image.");
        }

        var user = await _context.Users.FindAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found");

        var avatar = await _context.UserAvatars.FindAsync(userId);
        var now = DateTime.UtcNow;
        if (avatar is null)
        {
            _context.UserAvatars.Add(new UserAvatar
            {
                UserId = userId,
                Data = webp,
                ContentType = ContentType,
                UpdatedAt = now,
            });
        }
        else
        {
            avatar.Data = webp;
            avatar.ContentType = ContentType;
            avatar.UpdatedAt = now;
        }

        user.AvatarUrl = $"/api/users/{userId}/avatar?v={now.Ticks}";
        user.UpdatedAt = now;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Avatar saved for user {UserId} ({Bytes} bytes)", userId, webp.Length);
        return user.AvatarUrl;
    }

    public async Task RemoveAsync(int userId)
    {
        var avatar = await _context.UserAvatars.FindAsync(userId);
        if (avatar is not null) _context.UserAvatars.Remove(avatar);

        var user = await _context.Users.FindAsync(userId);
        if (user is not null)
        {
            user.AvatarUrl = null;
            user.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Avatar removed for user {UserId}", userId);
    }
}
