namespace TaleTrackApp.Features.Stats.GetStats;

using System.ComponentModel.DataAnnotations;

public class GetStatsRequest
{
    [Range(2000, 3000, ErrorMessage = "Year must be between 2000 and 3000")]
    public int? Year { get; set; }
}
