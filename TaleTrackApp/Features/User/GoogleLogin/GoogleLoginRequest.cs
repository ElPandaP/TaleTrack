using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.User.GoogleLogin;

public class GoogleLoginRequest
{
    [Required]
    public required string IdToken { get; set; }

    /// <summary>Requester UI locale ("es"/"en"); used for the welcome email on a new account.</summary>
    [StringLength(10)]
    public string? Locale { get; set; }
}
