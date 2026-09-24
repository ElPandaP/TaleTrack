namespace TaleTrackApp.Features.Library.GetLibrary;

using System.ComponentModel.DataAnnotations;

public class GetLibraryRequest
{
    /// <summary>Only this media type: `Movie`, `Series` or `Book`.</summary>
    [RegularExpression(@"^(Movie|Series|Book)?$", ErrorMessage = "Type must be 'Movie', 'Series', 'Book' or empty")]
    public string? Type { get; set; }

    /// <summary>`finished` (100% progress) or `in_progress` (anything else, including no recorded progress).</summary>
    [RegularExpression(@"^(in_progress|finished)?$", ErrorMessage = "Status must be 'in_progress', 'finished' or empty")]
    public string? Status { get; set; }

    /// <summary>`recent` (default: latest activity first) or `rating` (the caller's rating, highest first).</summary>
    [RegularExpression(@"^(recent|rating)?$", ErrorMessage = "Sort must be 'recent', 'rating' or empty")]
    public string? Sort { get; set; }

    /// <summary>Only media whose latest activity was in this year.</summary>
    [Range(2000, 3000, ErrorMessage = "Year must be between 2000 and 3000")]
    public int? Year { get; set; }

    /// <summary>Maximum rows to return, 1-200. `total` in the response still counts every match.</summary>
    [Range(1, 200, ErrorMessage = "Limit must be between 1 and 200")]
    public int? Limit { get; set; }
}
