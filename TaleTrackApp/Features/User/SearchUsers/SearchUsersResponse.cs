namespace TaleTrackApp.Features.User.SearchUsers;

public class SearchUsersResponse
{
    public bool Success { get; set; }
    public FoundUser? User { get; set; }
    public string? Relationship { get; set; }
}

public class FoundUser
{
    public Guid UserId { get; set; }
    public required string Username { get; set; }
    public string? AvatarUrl { get; set; }
}
