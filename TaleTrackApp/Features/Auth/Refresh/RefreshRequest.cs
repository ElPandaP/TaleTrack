using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.Refresh;

public class RefreshRequest
{
    /// <summary>The current refresh token.</summary>
    [Required(ErrorMessage = "refreshToken is required")]
    public required string RefreshToken { get; set; }
}
