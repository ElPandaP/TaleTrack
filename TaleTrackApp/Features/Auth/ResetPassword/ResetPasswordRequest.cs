using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.ResetPassword;

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "token is required")]
    public required string Token { get; set; }

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
    public required string Password { get; set; }
}
