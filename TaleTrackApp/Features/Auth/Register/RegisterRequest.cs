namespace TaleTrackApp.Features.Auth.Register;

using System.ComponentModel.DataAnnotations;

/// <summary>Body of `POST /api/register`.</summary>
public class RegisterRequest
{
    /// <summary>Account email. Must not be registered yet.</summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email must be valid")]
    public required string Email { get; set; }
    
    /// <summary>Public username, 3-50 characters. Must not be taken.</summary>
    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    public required string Username { get; set; }
    
    /// <summary>Password, following <see cref="PasswordPolicy"/>.</summary>
    [Required(ErrorMessage = "Password is required")]
    [RegularExpression(PasswordPolicy.Pattern, ErrorMessage = PasswordPolicy.Message)]
    public required string Password { get; set; }

    /// <summary>Requester UI locale ("es"/"en"); the welcome email is sent in it.</summary>
    [StringLength(10)]
    public string? Locale { get; set; }
}
