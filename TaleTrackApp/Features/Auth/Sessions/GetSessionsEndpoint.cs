using System.Security.Claims;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.Auth.Sessions;

/// <summary>Lists the caller's active devices/sessions for the "Conexiones" page.</summary>
public static class GetSessionsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/auth/sessions", HandleAsync)
            .WithName("GetSessions")
            .WithDescription("The authenticated user's active refresh-token sessions")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        RefreshTokenService refreshTokens,
        ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Results.Unauthorized();

        var sessions = await refreshTokens.ListActiveAsync(userId);

        return Results.Ok(new
        {
            success = true,
            data = sessions.Select(s => new
            {
                id = s.Id,
                device = s.Device,
                createdAt = s.CreatedAt,
                lastUsedAt = s.LastUsedAt,
                expiresAt = s.ExpiresAt,
            }),
        });
    }
}
