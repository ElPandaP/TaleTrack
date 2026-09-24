namespace TaleTrackApp.Features.Auth.Login;

using System.ComponentModel.DataAnnotations;

public class LoginRequest
{
    /// <summary>Account email.</summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email must be valid")]
    public required string Email { get; set; }
    
    /// <summary>Account password (at least 6 characters).</summary>
    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public required string Password { get; set; }
}
