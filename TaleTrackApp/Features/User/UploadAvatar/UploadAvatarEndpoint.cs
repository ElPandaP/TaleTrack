using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.User.UploadAvatar;

public static class UploadAvatarEndpoint
{
    private const long MaxBytes = 5 * 1024 * 1024;

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/users/me/avatar", HandleAsync)
            .WithName("UploadAvatar")
            .WithTags("Users")
            .WithSummary("Upload the caller's profile photo")
            .WithDescription("Multipart form with a `file` field: an image up to 5 MB. It is cropped to a 256x256 square and stored as WebP.")
            .Responds<UploadAvatarResponse>("Photo saved; `avatarUrl` is cache-busted.")
            .RespondsBadRequest("Code `no_file`, `too_large` (over 5 MB) or `invalid_image`.")
            .DisableAntiforgery()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        IFormFile? file,
        AvatarService avatarService,
        ClaimsPrincipal principal,
        ILogger<AvatarService> logger)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
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
            return Results.Ok(new UploadAvatarResponse { Success = true, AvatarUrl = url });
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
