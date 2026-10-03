using System.Security.Claims;

namespace TaleTrackApp.Security;

/// <summary>Helpers for reading the authenticated user from the request's claims.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Reads the caller's user id from the <see cref="ClaimTypes.NameIdentifier"/> claim of their
    /// access token.
    /// </summary>
    /// <param name="principal">The request's user.</param>
    /// <param name="userId">The user id, or <see cref="Guid.Empty"/> when there is none.</param>
    /// <returns>
    /// False when the claim is missing or not a Guid, e.g. for a token that is not an access token;
    /// endpoints answer 401 then.
    /// </returns>
    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out userId);
    }
}
