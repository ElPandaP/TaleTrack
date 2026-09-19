using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.RevokeSession;

/// <summary>Revokes one of the caller's sessions (e.g. a lost device).</summary>
public static class RevokeSessionEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/auth/sessions/{id:guid}", HandleAsync)
            .WithName("RevokeSession")
            .WithDescription("Revokes one of the authenticated user's sessions")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        SessionService refreshTokens,
        ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        var revoked = await refreshTokens.RevokeAsync(userId, id);
        return revoked
            ? Results.Ok(new { success = true })
            : Results.NotFound(new { success = false, message = "Session not found." });
    }
}
