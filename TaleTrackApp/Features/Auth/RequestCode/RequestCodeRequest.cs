using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.RequestCode;

public class RequestCodeRequest
{
    /// <summary>Email of the account to sign in to.</summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email must be valid")]
    public required string Email { get; set; }

    /// <summary>Requester UI locale ("es"/"en"); the verification email is sent in it.</summary>
    [StringLength(10)]
    public string? Locale { get; set; }
}
