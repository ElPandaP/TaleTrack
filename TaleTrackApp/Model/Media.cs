namespace TaleTrackApp.Model;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// A film, series or book. One row is shared by every user who tracks it: it is created the first
/// time someone tracks the title and later enriched with metadata from TMDB or Open Library.
/// Columns that only apply to one <see cref="MediaType"/> stay null for the others.
/// </summary>
public class Media
{
    /// <summary>Primary key.</summary>
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>English title. At least one of <see cref="TitleEN"/> and <see cref="TitleES"/> is
    /// always set, depending on the language the content was first registered in; titles in other
    /// or unknown languages (e.g. a book, or Netflix in a third language) are stored here.</summary>
    [StringLength(255, MinimumLength = 1, ErrorMessage = "TitleEN must be between 1 and 255 characters")]
    public string? TitleEN { get; set; }

    /// <summary>Spanish title. Filled either directly (content registered from a Spanish
    /// Netflix UI) or later via TMDB translation once the other language is known.</summary>
    [StringLength(255, MinimumLength = 1, ErrorMessage = "TitleES must be between 1 and 255 characters")]
    public string? TitleES { get; set; }

    /// <summary>Synopsis, when the metadata provider returns one.</summary>
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }
    
    /// <summary>Whether this is a film, a series or a book; decides which optional columns may be filled.</summary>
    public required MediaType Type { get; set; }
    
    /// <summary>Runtime in minutes for a film or page count for a book; 0 when unknown, and always 0
    /// for a series.</summary>
    public int Length { get; set; }
    
    /// <summary>Absolute URL of the cover or poster image.</summary>
    [Url(ErrorMessage = "Invalid URL format for PosterUrl")]
    [StringLength(2048, ErrorMessage = "PosterUrl cannot exceed 2048 characters")]
    public string? PosterUrl { get; set; }

    /// <summary>Author of a book. Books only (enforced by a check constraint).</summary>
    [StringLength(255)]
    public string? Author { get; set; }

    /// <summary>ISBN of a book, used first when deduplicating books. Books only (enforced by a check constraint).</summary>
    [StringLength(13)]
    public string? Isbn { get; set; }

    /// <summary>Episode count per season (index 0 = season 1), from TMDB. Series only (enforced by a check constraint), null until enriched.</summary>
    public int[]? SeasonEpisodeCounts { get; set; }

    /// <summary>When the media was first tracked by anyone (UTC).</summary>
    [Required]
    public DateTime FirstTrackedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Last time the metadata was changed (UTC); null if never.</summary>
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>Reviews written about this media.</summary>
    public ICollection<Review> Reviews { get; set; } = new List<Review>();

    /// <summary>Every user's progress on this media.</summary>
    public ICollection<TrackingEvent> TrackingEvents { get; set; } = new List<TrackingEvent>();
}