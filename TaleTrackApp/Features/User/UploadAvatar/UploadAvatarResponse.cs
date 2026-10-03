namespace TaleTrackApp.Features.User.UploadAvatar;

/// <summary>Body returned by <c>PUT /api/users/me/avatar</c>.</summary>
public class UploadAvatarResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>URL of the new photo. It changes with every upload, so it can be cached indefinitely.</summary>
    public required string AvatarUrl { get; set; }
}
