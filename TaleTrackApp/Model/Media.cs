namespace TaleTrackApp.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Media
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>English title. At least one of TitleEN/TitleES is always set — which one
    /// depends on the language the content was first registered in; unsupported/unknown
    /// languages (e.g. a book, or Netflix in a third language) default here.</summary>
    [StringLength(255, MinimumLength = 1, ErrorMessage = "TitleEN must be between 1 and 255 characters")]
    public string? TitleEN { get; set; }

    /// <summary>Spanish title. Filled either directly (content registered from a Spanish
    /// Netflix UI) or later via TMDB translation once the other language is known.</summary>
    [StringLength(255, MinimumLength = 1, ErrorMessage = "TitleES must be between 1 and 255 characters")]
    public string? TitleES { get; set; }

    /// <summary>Display fallback for logging — the frontend picks EN/ES itself per viewer locale.</summary>
    [NotMapped]
    public string Title => TitleEN ?? TitleES ?? string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }
    
    public required MediaType Type { get; set; }
    
    [Range(1, int.MaxValue, ErrorMessage = "Length must be greater than 0")]
    public int Length { get; set; }
    
    [Url(ErrorMessage = "Invalid URL format for PosterUrl")]
    [StringLength(2048, ErrorMessage = "PosterUrl cannot exceed 2048 characters")]
    public string? PosterUrl { get; set; }

    /// <summary>Books only (enforced by a check constraint).</summary>
    [StringLength(255)]
    public string? Author { get; set; }

    /// <summary>Books only (enforced by a check constraint).</summary>
    [StringLength(13)]
    public string? Isbn { get; set; }

    /// <summary>Episode count per season (index 0 = season 1), from TMDB. Series only (enforced by a check constraint), null until enriched.</summary>
    public int[]? SeasonEpisodeCounts { get; set; }

    [Required]
    public DateTime FirstTrackedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<TrackingEvent> TrackingEvents { get; set; } = new List<TrackingEvent>();
}