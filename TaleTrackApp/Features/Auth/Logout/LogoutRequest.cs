using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.Logout;

public class LogoutRequest
{
    /// <summary>Refresh token of the session to end.</summary>
    [Required(ErrorMessage = "refreshToken is required")]
    public required string RefreshToken { get; set; }
}
