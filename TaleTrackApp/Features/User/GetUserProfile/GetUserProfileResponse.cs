namespace TaleTrackApp.Features.User.GetUserProfile;

public class GetUserProfileResponse
{
    public bool Success { get; set; }
    public required UserProfileData Data { get; set; }
}

public class UserProfileData
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public required string Relationship { get; set; }
    public required MediaCounts Counts { get; set; }
}

public class MediaCounts
{
    public int Book { get; set; }
    public int Movie { get; set; }
    public int Series { get; set; }
    public int Total { get; set; }
}
