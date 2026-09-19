namespace TaleTrackApp.Features.Auth.GetSessions;

public class GetSessionsResponse
{
    public bool Success { get; set; }
    public required List<SessionItem> Data { get; set; }
}

public class SessionItem
{
    public Guid Id { get; set; }
    public required string Device { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
