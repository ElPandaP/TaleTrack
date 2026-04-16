namespace TaleTrackApp.Features.TrackingEvent.GetTrackingEvents;

using System.ComponentModel.DataAnnotations;

public class GetTrackingEventsRequest
{
    [StringLength(20, ErrorMessage = "Type no puede exceder 20 caracteres")]
    [RegularExpression(@"^(Movie|Series|Book)?$", ErrorMessage = "Type debe ser 'Movie', 'Series', 'Book' o vacío")]
    public string? Type { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Limit debe ser mayor que 0")]
    public int? Limit { get; set; }

    [StringLength(20, ErrorMessage = "OrderBy no puede exceder 20 caracteres")]
    [RegularExpression(@"^(title_asc|title_desc|date_asc|date_desc)?$", ErrorMessage = "OrderBy debe ser: title_asc, title_desc, date_asc, date_desc")]
    public string? OrderBy { get; set; }
}
