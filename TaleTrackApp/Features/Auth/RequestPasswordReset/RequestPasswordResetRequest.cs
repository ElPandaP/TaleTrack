using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.RequestPasswordReset;

public class RequestPasswordResetRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email must be valid")]
    public required string Email { get; set; }

    /// <summary>UI locale of the requester ("es"/"en"); anything else is treated as English.</summary>
    [StringLength(10)]
    public string? Locale { get; set; }
}
