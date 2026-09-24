using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.GoogleSignupComplete;

public class GoogleSignupCompleteRequest
{
    /// <summary>The `pendingToken` returned by `POST /api/auth/google`.</summary>
    [Required]
    public required string PendingToken { get; set; }

    /// <summary>Chosen username, 3-50 characters. Must not be taken.</summary>
    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    public required string Username { get; set; }

    /// <summary>Requester UI locale ("es"/"en"); used for the welcome email.</summary>
    [StringLength(10)]
    public string? Locale { get; set; }
}
