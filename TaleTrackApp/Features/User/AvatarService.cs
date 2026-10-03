using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using TaleTrackApp.Data;
using TaleTrackApp.Model;

namespace TaleTrackApp.Features.User;

/// <summary>Thrown when an uploaded file isn't a decodable image.</summary>
public class InvalidImageException(string message) : Exception(message);

/// <summary>
/// Stores profile photos. Each photo is normalized to a small square WebP image and kept in the
/// database, served by <c>GET /api/users/{id}/avatar</c>.
/// </summary>
public class AvatarService
{
    /// <summary>Width and height, in pixels, of every stored photo.</summary>
    private const int Size = 256;
    /// <summary>Content type of every stored photo.</summary>
    private const string ContentType = "image/webp";

    /// <summary>Database context used to read and write photos and users.</summary>
    private readonly AppDbContext _context;
    /// <summary>Logger for this service.</summary>
    private readonly ILogger<AvatarService> _logger;

    /// <summary>Creates the service with its database context and logger.</summary>
    public AvatarService(AppDbContext context, ILogger<AvatarService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>The user's stored photo, or null if they have none.</summary>
    public Task<UserAvatar?> GetAsync(Guid userId) =>
        _context.UserAvatars.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId);

    /// <summary>
    /// Decodes the image, centre-crops it to a <see cref="Size"/>x<see cref="Size"/> square,
    /// re-encodes it as WebP, stores it and points the user's <c>AvatarUrl</c> at it.
    /// </summary>
    /// <returns>The new avatar URL, which changes with every upload so browsers can cache it forever.</returns>
    /// <exception cref="InvalidImageException">The stream is not an image format that can be decoded.</exception>
    public async Task<string> SaveAsync(Guid userId, Stream imageStream)
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

    /// <summary>Deletes the user's photo and clears their <c>AvatarUrl</c>. Does nothing if there is no photo.</summary>
    public async Task RemoveAsync(Guid userId)
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
