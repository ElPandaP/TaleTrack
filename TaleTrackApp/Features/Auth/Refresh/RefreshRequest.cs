using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.Refresh;

public class RefreshRequest
{
    [Required(ErrorMessage = "refreshToken is required")]
    public required string RefreshToken { get; set; }
}
