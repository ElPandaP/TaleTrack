namespace TaleTrackApp.Features.User.UploadAvatar;

public class UploadAvatarResponse
{
    public bool Success { get; set; }
    public required string AvatarUrl { get; set; }
}
