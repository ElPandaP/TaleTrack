using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.ResetPassword;

/// <summary>Body of `POST /api/auth/reset-password`.</summary>
public class ResetPasswordRequest
{
    /// <summary>Single-use token from the password-reset email.</summary>
    [Required(ErrorMessage = "token is required")]
    public required string Token { get; set; }

    /// <summary>The new password, following <see cref="PasswordPolicy"/>.</summary>
    [Required(ErrorMessage = "Password is required")]
    [RegularExpression(PasswordPolicy.Pattern, ErrorMessage = PasswordPolicy.Message)]
    public required string Password { get; set; }
}
