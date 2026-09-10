namespace TaleTrackApp.Features.Media.GetMedia;

using System.ComponentModel.DataAnnotations;

public class GetMediaRequest
{
    [StringLength(20, ErrorMessage = "Type cannot exceed 20 characters")]
    [RegularExpression(@"^(Movie|Series|Book)?$", ErrorMessage = "Type must be 'Movie', 'Series', 'Book' or empty")]
    public string? Type { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Limit must be greater than 0")]
    public int? Limit { get; set; }

    [StringLength(20, ErrorMessage = "OrderBy cannot exceed 20 characters")]
    [RegularExpression(@"^(title_asc|title_desc|date_asc|date_desc)?$", ErrorMessage = "OrderBy must be one of: title_asc, title_desc, date_asc, date_desc")]
    public string? OrderBy { get; set; }
}
