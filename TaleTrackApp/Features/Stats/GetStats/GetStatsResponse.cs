namespace TaleTrackApp.Features.Stats.GetStats;

public class GetStatsResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>The yearly summary.</summary>
    public required YearlyStats Data { get; set; }
}
