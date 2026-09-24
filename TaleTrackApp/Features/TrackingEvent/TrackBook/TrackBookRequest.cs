namespace TaleTrackApp.Features.TrackingEvent.TrackBook;

using System.ComponentModel.DataAnnotations;

public class TrackBookRequest
{
    /// <summary>Book title.</summary>
    [Required(ErrorMessage = "Title is required")]
    [StringLength(255, MinimumLength = 1)]
    public required string Title { get; set; }

    /// <summary>Author. Together with the title it identifies an existing book.</summary>
    [StringLength(255)]
    public string? Author { get; set; }

    /// <summary>ISBN, up to 13 characters. The strongest way to identify an existing book.</summary>
    [StringLength(13)]
    public string? Isbn { get; set; }

    /// <summary>Page count.</summary>
    [Range(1, int.MaxValue)]
    public int? Pages { get; set; }

    /// <summary>Percentage read, 0-100.</summary>
    [Range(0, 100)]
    public int? Progress { get; set; }
}
