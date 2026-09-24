namespace TaleTrackApp.Features.Stats.GetStats;

using System.ComponentModel.DataAnnotations;

public class GetStatsRequest
{
    /// <summary>Year to summarize, 2000-3000. Defaults to the current year.</summary>
    [Range(2000, 3000, ErrorMessage = "Year must be between 2000 and 3000")]
    public int? Year { get; set; }
}
