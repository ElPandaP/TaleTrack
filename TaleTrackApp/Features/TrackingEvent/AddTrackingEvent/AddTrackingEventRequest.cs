namespace TaleTrackApp.Features.TrackingEvent.AddTrackingEvent;

using System.ComponentModel.DataAnnotations;

public class AddTrackingEventRequest
{
    [Required(ErrorMessage = "Título es requerido")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Título debe tener entre 1 y 255 caracteres")]
    public required string Title { get; set; }
    
    [Required(ErrorMessage = "Type es requerido")]
    [RegularExpression(@"^(Movie|Series|Book)$", ErrorMessage = "Type debe ser 'Movie', 'Series' o 'Book'")]
    public required string Type { get; set; }
    
    [Range(1, int.MaxValue, ErrorMessage = "Length debe ser mayor que 0")]
    public int Length { get; set; }
    
    [Range(0, 100, ErrorMessage = "Progress debe estar entre 0 y 100")]
    public int? Progress { get; set; }

    [StringLength(255)]
    public string? Author { get; set; }

    [StringLength(13)]
    public string? Isbn { get; set; }
}
