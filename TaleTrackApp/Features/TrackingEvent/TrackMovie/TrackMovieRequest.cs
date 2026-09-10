namespace TaleTrackApp.Features.TrackingEvent.TrackMovie;

using System.ComponentModel.DataAnnotations;

public class TrackMovieRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(255, MinimumLength = 1)]
    public required string Title { get; set; }

    /// <summary>Runtime in minutes.</summary>
    [Range(1, int.MaxValue)]
    public int? Minutes { get; set; }

    [Range(0, 100)]
    public int? Progress { get; set; }

    /// <summary>Netflix UI language ("es"/"en") the title was scraped in — used for TMDB enrichment.</summary>
    [StringLength(5)]
    public string? Language { get; set; }
}
