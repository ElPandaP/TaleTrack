using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.ConfirmDelete;

public class ConfirmDeleteRequest
{
    /// <summary>Single-use token from the account-deletion confirmation email.</summary>
    [Required(ErrorMessage = "token is required")]
    public required string Token { get; set; }
}
