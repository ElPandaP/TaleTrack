namespace TaleTrackApp.Features.Media.AddMedia;

using System.ComponentModel.DataAnnotations;

public class AddMediaRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 255 characters")]
    public required string Title { get; set; }
    
    [Required(ErrorMessage = "Type is required")]
    [RegularExpression(@"^(Movie|Series|Book)$", ErrorMessage = "Type must be 'Movie', 'Series' or 'Book'")]
    public required string Type { get; set; }
    
    [Range(1, int.MaxValue, ErrorMessage = "Length must be greater than 0")]
    public int Length { get; set; }
}
