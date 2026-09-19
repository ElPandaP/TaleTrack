using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.Logout;

public class LogoutRequest
{
    [Required(ErrorMessage = "refreshToken is required")]
    public required string RefreshToken { get; set; }
}
