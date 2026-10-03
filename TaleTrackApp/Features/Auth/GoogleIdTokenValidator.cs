using Google.Apis.Auth;

namespace TaleTrackApp.Features.Auth;

/// <summary>
/// Validates a Google id token against Google's signing keys. Kept apart from
/// <see cref="GoogleAuthService"/> so the integration tests can swap it for a fake, the same way
/// they swap the HTTP clients of TMDB and Open Library.
/// </summary>
public class GoogleIdTokenValidator
{
    /// <summary>Checks the token's signature, expiry and audience and returns its claims.</summary>
    /// <param name="idToken">The id token obtained by the client from Google Sign-In.</param>
    /// <param name="clientId">The app's Google client id, which must be the token's audience.</param>
    /// <returns>The claims of the token (Google id, email and name).</returns>
    /// <exception cref="InvalidJwtException">The token is not a valid Google id token for this app.</exception>
    public virtual Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken, string clientId) =>
        GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [clientId]
        });
}
