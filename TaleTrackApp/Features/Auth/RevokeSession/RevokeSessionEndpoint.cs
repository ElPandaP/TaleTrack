using TaleTrackApp.OpenApi;
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
            .WithTags("Auth")
            .WithSummary("Revoke one of the caller's sessions")
            .WithDescription("Signs that device out, e.g. after losing it. Its access token keeps working until it expires.")
            .Responds<ApiResult>("Session revoked.")
            .RespondsNotFound("There is no such session for the caller.")
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
