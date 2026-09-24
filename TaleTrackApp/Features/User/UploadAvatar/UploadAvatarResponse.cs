namespace TaleTrackApp.Features.User.UploadAvatar;

public class UploadAvatarResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>URL of the new photo. It changes with every upload, so it can be cached indefinitely.</summary>
    public required string AvatarUrl { get; set; }
}
