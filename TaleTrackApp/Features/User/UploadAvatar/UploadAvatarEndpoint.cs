using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.User.UploadAvatar;

/// <summary>
/// <c>PUT /api/users/me/avatar</c>: uploads the caller's profile photo as a multipart form with a
/// <c>file</c> field. Requires a valid JWT.
/// Validates and stores the photo and returns its new URL. Returns 400 with <c>no_file</c>, <c>too_large</c> or <c>invalid_image</c> when the upload is rejected, and 401 if the token has no valid user id.
/// </summary>
public static class UploadAvatarEndpoint
{
    /// <summary>Largest accepted upload, 5 MB.</summary>
    private const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>Registers the route on the <c>/api</c> group, behind the user policy.</summary>
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

    /// <summary>Upload the caller's profile photo</summary>
    private static async Task<IResult> HandleAsync(
        IFormFile? file,
        AvatarService avatarService,
        ClaimsPrincipal principal)
    {
        if (!principal.TryGetUserId(out var userId))
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
    }
}
