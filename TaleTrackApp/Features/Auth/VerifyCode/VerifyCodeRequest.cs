using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.VerifyCode;

/// <summary>Body of `POST /api/auth/verify-code`.</summary>
public class VerifyCodeRequest
{
    /// <summary>Email the code was sent to.</summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email must be valid")]
    public required string Email { get; set; }

    /// <summary>The 6-character code from the email.</summary>
    [Required(ErrorMessage = "The code is required")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "The code must be 6 characters long")]
    public required string Code { get; set; }

    /// <summary>Label of the new session in the sessions list (e.g. "Web"). Defaults to "KOReader" when empty.</summary>
    [StringLength(60, ErrorMessage = "The device name cannot exceed 60 characters")]
    public string? Device { get; set; }
}
