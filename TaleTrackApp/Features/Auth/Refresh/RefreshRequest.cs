using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.Refresh;

/// <summary>Body of `POST /api/auth/refresh`.</summary>
public class RefreshRequest
{
    /// <summary>The current refresh token.</summary>
    [Required(ErrorMessage = "refreshToken is required")]
    public required string RefreshToken { get; set; }
}
