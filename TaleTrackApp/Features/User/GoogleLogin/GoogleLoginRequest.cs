using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.User.GoogleLogin;

public class GoogleLoginRequest
{
    [Required]
    public required string IdToken { get; set; }
}
