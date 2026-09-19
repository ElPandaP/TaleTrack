using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.ConfirmDelete;

public class ConfirmDeleteRequest
{
    [Required(ErrorMessage = "token is required")]
    public required string Token { get; set; }
}
