namespace TaleTrackApp.Features.Stats.GetStats;

public class GetStatsResponse
{
    public bool Success { get; set; }
    public required YearlyStats Data { get; set; }
}
