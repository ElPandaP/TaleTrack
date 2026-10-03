using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.RevokeSignup;

/// <summary>Body of `POST /api/auth/revoke-signup`.</summary>
public class RevokeSignupRequest
{
    /// <summary>Single-use token from the welcome email.</summary>
    [Required(ErrorMessage = "token is required")]
    public required string Token { get; set; }
}
