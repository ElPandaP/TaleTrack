namespace TaleTrackApp.Features.TrackingEvent.TrackSeries;

using System.ComponentModel.DataAnnotations;

/// <summary>Body of <c>POST /api/tracking/series</c>.</summary>
public class TrackSeriesRequest
{
    /// <summary>Series title, without season or episode.</summary>
    [Required(ErrorMessage = "Title is required")]
    [StringLength(255, MinimumLength = 1)]
    public required string Title { get; set; }

    /// <summary>Season number, from 1.</summary>
    [Required(ErrorMessage = "Season is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Season must be 1 or greater")]
    public int? Season { get; set; }

    /// <summary>Episode number within the season, from 1.</summary>
    [Required(ErrorMessage = "Episode is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Episode must be 1 or greater")]
    public int? Episode { get; set; }

    /// <summary>Progress through the current episode, 0-100.</summary>
    [Range(0, 100)]
    public int? Progress { get; set; }

    /// <summary>Language of the title as shown by Netflix (<c>es</c> or <c>en</c>). Needed for the TMDB lookup; without it no metadata is fetched.</summary>
    [StringLength(5)]
    public string? Language { get; set; }
}
