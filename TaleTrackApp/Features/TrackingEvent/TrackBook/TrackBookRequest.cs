namespace TaleTrackApp.Features.TrackingEvent.TrackBook;

using System.ComponentModel.DataAnnotations;

public class TrackBookRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(255, MinimumLength = 1)]
    public required string Title { get; set; }

    [StringLength(255)]
    public string? Author { get; set; }

    [StringLength(13)]
    public string? Isbn { get; set; }

    /// <summary>Page count.</summary>
    [Range(1, int.MaxValue)]
    public int? Pages { get; set; }

    [Range(0, 100)]
    public int? Progress { get; set; }
}
