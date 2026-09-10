using System.Security.Claims;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.User.Avatar;

public static class UploadAvatarEndpoint
{
    private const long MaxBytes = 5 * 1024 * 1024;

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/user/avatar", HandleAsync)
            .WithName("UploadAvatar")
            .WithDescription("Uploads a profile photo; it is cropped to a 256x256 square (requires JWT + internal API key)")
            .DisableAntiforgery()
            .RequireAuthorization(Policies.UserPolicy)
            .RequireAuthorization(Policies.InternalOnly);
    }

    private static async Task<IResult> HandleAsync(
        IFormFile? file,
        AvatarService avatarService,
        ClaimsPrincipal principal,
        ILogger<AvatarService> logger)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Results.Unauthorized();

        if (file is null || file.Length == 0)
            return Results.BadRequest(new { code = "no_file", message = "No file was uploaded." });

        if (file.Length > MaxBytes)
            return Results.BadRequest(new { code = "too_large", message = "The image must be 5 MB or smaller." });

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { code = "invalid_image", message = "The file is not an image." });

        try
        {
            await using var stream = file.OpenReadStream();
            var url = await avatarService.SaveAsync(userId, stream);
            return Results.Ok(new { success = true, avatarUrl = url });
        }
        catch (InvalidImageException)
        {
            return Results.BadRequest(new { code = "invalid_image", message = "The file is not a supported image." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Avatar upload failed for user {UserId}", userId);
            return Results.StatusCode(500);
        }
    }
}
