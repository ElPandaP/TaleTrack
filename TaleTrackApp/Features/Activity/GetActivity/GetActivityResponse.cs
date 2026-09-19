namespace TaleTrackApp.Features.Activity.GetActivity;

public class GetActivityResponse
{
    public bool Success { get; set; }
    public int Count { get; set; }
    public required List<ActivityItem> Data { get; set; }
}
