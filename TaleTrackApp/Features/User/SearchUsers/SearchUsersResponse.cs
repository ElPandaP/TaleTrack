namespace TaleTrackApp.Features.User.SearchUsers;

/// <summary>Body returned by <c>GET /api/users/search</c>.</summary>
public class SearchUsersResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>The matching user. Absent when nobody has that username.</summary>
    public FoundUser? User { get; set; }
    /// <summary>How the user relates to the caller: `self`, `friends`, `incoming` (they asked the caller), `outgoing` (the caller asked them) or `none`.</summary>
    public string? Relationship { get; set; }
}

/// <summary>The public data of the user found.</summary>
public class FoundUser
{
    /// <summary>User id.</summary>
    public Guid UserId { get; set; }
    /// <summary>Public username.</summary>
    public required string Username { get; set; }
    /// <summary>URL of the profile photo. Null when the user has none.</summary>
    public string? AvatarUrl { get; set; }
}
