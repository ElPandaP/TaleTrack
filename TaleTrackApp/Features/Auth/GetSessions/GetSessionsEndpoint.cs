using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.GetSessions;

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
        SessionService refreshTokens,
        ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        var sessions = await refreshTokens.ListActiveAsync(userId);

        return Results.Ok(new GetSessionsResponse
        {
            Success = true,
            Data = sessions.Select(s => new SessionItem
            {
                Id = s.Id,
                Device = s.Device,
                CreatedAt = s.CreatedAt,
                LastUsedAt = s.LastUsedAt,
                ExpiresAt = s.ExpiresAt,
            }).ToList(),
        });
    }
}
