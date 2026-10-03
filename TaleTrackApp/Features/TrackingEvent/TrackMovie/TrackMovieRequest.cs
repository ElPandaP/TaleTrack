namespace TaleTrackApp.Features.TrackingEvent.TrackMovie;

using System.ComponentModel.DataAnnotations;

/// <summary>Body of <c>POST /api/tracking/movies</c>.</summary>
public class TrackMovieRequest
{
    /// <summary>Film title.</summary>
    [Required(ErrorMessage = "Title is required")]
    [StringLength(255, MinimumLength = 1)]
    public required string Title { get; set; }

    /// <summary>Runtime in minutes.</summary>
    [Range(1, int.MaxValue)]
    public int? Minutes { get; set; }

    /// <summary>Percentage watched, 0-100.</summary>
    [Range(0, 100)]
    public int? Progress { get; set; }

    /// <summary>Language of the title as shown by Netflix (<c>es</c> or <c>en</c>). Needed for the TMDB lookup; without it no metadata is fetched.</summary>
    [StringLength(5)]
    public string? Language { get; set; }
}
