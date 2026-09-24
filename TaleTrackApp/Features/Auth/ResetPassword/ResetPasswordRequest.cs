using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.ResetPassword;

public class ResetPasswordRequest
{
    /// <summary>Single-use token from the password-reset email.</summary>
    [Required(ErrorMessage = "token is required")]
    public required string Token { get; set; }

    /// <summary>The new password, 6-100 characters.</summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
    public required string Password { get; set; }
}
