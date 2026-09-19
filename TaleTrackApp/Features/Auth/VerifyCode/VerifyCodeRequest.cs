using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.VerifyCode;

public class VerifyCodeRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email must be valid")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "The code is required")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "The code must be 6 characters long")]
    public required string Code { get; set; }

    /// <summary>Session label for the issued refresh token; defaults to "KOReader", this
    /// endpoint's original caller, when the client doesn't say otherwise (e.g. "Web").</summary>
    public string? Device { get; set; }
}
