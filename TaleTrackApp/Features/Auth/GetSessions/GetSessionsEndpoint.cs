using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.GetSessions;

/// <summary>
/// <c>GET /api/auth/sessions</c>: lists the caller's active sessions (one per signed-in device) for
/// the "Conexiones" page. Requires a valid JWT.
/// </summary>
public static class GetSessionsEndpoint
{
    /// <summary>Registers the endpoint on the <c>/api</c> route group.</summary>
    /// <param name="group">The <c>/api</c> group.</param>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/auth/sessions", HandleAsync)
            .WithName("GetSessions")
            .WithTags("Auth")
            .WithSummary("List the caller's active sessions")
            .WithDescription("One row per signed-in device (one per non-revoked, non-expired refresh token).")
            .Responds<GetSessionsResponse>("The active sessions.")
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>List the caller's active sessions</summary>
    private static async Task<IResult> HandleAsync(
        SessionService refreshTokens,
        ClaimsPrincipal principal)
    {
        if (!principal.TryGetUserId(out var userId))
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
