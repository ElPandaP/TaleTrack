using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.RevokeSession;

/// <summary>
/// <c>DELETE /api/auth/sessions/{id}</c>: revokes one of the caller's sessions (e.g. a lost device).
/// Requires a valid JWT. Returns 404 if the session does not exist or belongs to someone else.
/// </summary>
public static class RevokeSessionEndpoint
{
    /// <summary>Registers the endpoint on the <c>/api</c> route group.</summary>
    /// <param name="group">The <c>/api</c> group.</param>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/auth/sessions/{id:guid}", HandleAsync)
            .WithName("RevokeSession")
            .WithTags("Auth")
            .WithSummary("Revoke one of the caller's sessions")
            .WithDescription("Signs that device out, e.g. after losing it. Its access token keeps working until it expires, a few minutes at most.")
            .Responds<ApiResult>("Session revoked.")
            .RespondsNotFound("There is no such session for the caller.")
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Revoke one of the caller's sessions</summary>
    private static async Task<IResult> HandleAsync(
        Guid id,
        SessionService refreshTokens,
        ClaimsPrincipal principal)
    {
        if (!principal.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var revoked = await refreshTokens.RevokeAsync(userId, id);
        return revoked
            ? Results.Ok(new { success = true })
            : Results.NotFound(new { success = false, message = "Session not found." });
    }
}
