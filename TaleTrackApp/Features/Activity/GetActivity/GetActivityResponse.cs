namespace TaleTrackApp.Features.Activity.GetActivity;

/// <summary>Body returned by <c>GET /api/activity</c>.</summary>
public class GetActivityResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>Number of entries in `data`.</summary>
    public int Count { get; set; }
    /// <summary>Feed entries, newest first.</summary>
    public required List<ActivityItem> Data { get; set; }
}
