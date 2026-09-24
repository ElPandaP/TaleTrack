namespace TaleTrackApp.Features.Auth.GetSessions;

public class GetSessionsResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>The caller's active sessions.</summary>
    public required List<SessionItem> Data { get; set; }
}

public class SessionItem
{
    /// <summary>Session id. Pass it to `DELETE /api/auth/sessions/{id}` to revoke the session.</summary>
    public Guid Id { get; set; }
    /// <summary>Label of the client that signed in, e.g. `Web`, `KOReader` or `Netflix extension`.</summary>
    public required string Device { get; set; }
    /// <summary>When the session was started.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>Last time the session's refresh token was used.</summary>
    public DateTime LastUsedAt { get; set; }
    /// <summary>When the session's refresh token expires.</summary>
    public DateTime ExpiresAt { get; set; }
}
