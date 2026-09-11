using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.User.GoogleSignupComplete;

public class GoogleSignupCompleteRequest
{
    [Required]
    public required string PendingToken { get; set; }

    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    public required string Username { get; set; }

    /// <summary>Requester UI locale ("es"/"en"); used for the welcome email.</summary>
    [StringLength(10)]
    public string? Locale { get; set; }
}
