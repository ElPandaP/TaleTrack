using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.GoogleLogin;

/// <summary>Body of `POST /api/auth/google`.</summary>
public class GoogleLoginRequest
{
    /// <summary>Google ID token obtained on the client with Google Sign-In.</summary>
    [Required]
    public required string IdToken { get; set; }
}
