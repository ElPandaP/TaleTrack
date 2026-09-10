namespace TaleTrackApp.Features.Library.GetLibrary;

using System.ComponentModel.DataAnnotations;

public class GetLibraryRequest
{
    [RegularExpression(@"^(Movie|Series|Book)?$", ErrorMessage = "Type must be 'Movie', 'Series', 'Book' or empty")]
    public string? Type { get; set; }

    [RegularExpression(@"^(in_progress|finished)?$", ErrorMessage = "Status must be 'in_progress', 'finished' or empty")]
    public string? Status { get; set; }

    [RegularExpression(@"^(recent|rating)?$", ErrorMessage = "Sort must be 'recent', 'rating' or empty")]
    public string? Sort { get; set; }

    [Range(2000, 3000, ErrorMessage = "Year must be between 2000 and 3000")]
    public int? Year { get; set; }

    [Range(1, 200, ErrorMessage = "Limit must be between 1 and 200")]
    public int? Limit { get; set; }
}
