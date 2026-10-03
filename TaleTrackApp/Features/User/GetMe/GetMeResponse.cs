namespace TaleTrackApp.Features.User.GetMe;

/// <summary>Body returned by <c>GET /api/users/me</c>.</summary>
public class GetMeResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>The caller's profile.</summary>
    public required MeData Data { get; set; }
}

/// <summary>The caller's own profile, with the private fields only they can see.</summary>
public class MeData
{
    /// <summary>User id.</summary>
    public Guid Id { get; set; }
    /// <summary>Public username.</summary>
    public required string Username { get; set; }
    /// <summary>Account email. Private: never shown to other users.</summary>
    public required string Email { get; set; }
    /// <summary>URL of the profile photo. Null when the user has none.</summary>
    public string? AvatarUrl { get; set; }
    /// <summary>When the account was created.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>What friends can see of the caller in the activity feed.</summary>
    public required FeedPrivacyData Privacy { get; set; }
}

/// <summary>Which of the caller's activity their friends can see in the feed, per media type.</summary>
public class FeedPrivacyData
{
    /// <summary>Friends see the caller's book progress.</summary>
    public bool BookProgress { get; set; }
    /// <summary>Friends see the caller's book reviews.</summary>
    public bool BookReviews { get; set; }
    /// <summary>Friends see the caller's film progress.</summary>
    public bool MovieProgress { get; set; }
    /// <summary>Friends see the caller's film reviews.</summary>
    public bool MovieReviews { get; set; }
    /// <summary>Friends see the caller's series progress.</summary>
    public bool SeriesProgress { get; set; }
    /// <summary>Friends see the caller's series reviews.</summary>
    public bool SeriesReviews { get; set; }
}
