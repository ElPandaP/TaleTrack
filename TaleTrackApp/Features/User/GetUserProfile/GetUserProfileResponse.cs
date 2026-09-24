namespace TaleTrackApp.Features.User.GetUserProfile;

public class GetUserProfileResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>The public profile.</summary>
    public required UserProfileData Data { get; set; }
}

public class UserProfileData
{
    /// <summary>User id.</summary>
    public Guid Id { get; set; }
    /// <summary>Public username.</summary>
    public required string Username { get; set; }
    /// <summary>URL of the profile photo. Null when the user has none.</summary>
    public string? AvatarUrl { get; set; }
    /// <summary>When the account was created.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>How the user relates to the caller: `self`, `friends`, `incoming` (they asked the caller), `outgoing` (the caller asked them) or `none`.</summary>
    public required string Relationship { get; set; }
    /// <summary>How many media of each type the user tracks.</summary>
    public required MediaCounts Counts { get; set; }
}

public class MediaCounts
{
    /// <summary>Books tracked.</summary>
    public int Book { get; set; }
    /// <summary>Films tracked.</summary>
    public int Movie { get; set; }
    /// <summary>Series tracked.</summary>
    public int Series { get; set; }
    /// <summary>All media tracked.</summary>
    public int Total { get; set; }
}
