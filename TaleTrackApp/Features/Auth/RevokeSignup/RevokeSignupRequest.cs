using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.RevokeSignup;

public class RevokeSignupRequest
{
    [Required(ErrorMessage = "token is required")]
    public required string Token { get; set; }
}
