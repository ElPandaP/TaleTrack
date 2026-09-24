namespace TaleTrackApp.Features.TrackingEvent.TrackSeries;

using System.ComponentModel.DataAnnotations;

public class TrackSeriesRequest
{
    /// <summary>Series title, without season or episode.</summary>
    [Required(ErrorMessage = "Title is required")]
    [StringLength(255, MinimumLength = 1)]
    public required string Title { get; set; }

    /// <summary>Season number.</summary>
    [Required(ErrorMessage = "Season is required")]
    [Range(0, int.MaxValue)]
    public int? Season { get; set; }

    /// <summary>Episode number within the season.</summary>
    [Required(ErrorMessage = "Episode is required")]
    [Range(0, int.MaxValue)]
    public int? Episode { get; set; }

    /// <summary>Episode runtime in minutes.</summary>
    [Range(1, int.MaxValue)]
    public int? Minutes { get; set; }

    /// <summary>Progress through the current episode, 0–100.</summary>
    [Range(0, 100)]
    public int? Progress { get; set; }

    /// <summary>Netflix UI language ("es"/"en") the title was scraped in — used for TMDB enrichment.</summary>
    [StringLength(5)]
    public string? Language { get; set; }
}
