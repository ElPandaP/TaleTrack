using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.Logout;

/// <summary>Revokes a single refresh token — used by clients to end their own session
/// (e.g. the browser extension's "sign out").</summary>
public static class LogoutEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/logout", HandleAsync)
            .WithName("Logout")
            .WithDescription("Revokes the supplied refresh token")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        LogoutRequest request,
        SessionService refreshTokens)
    {
        await refreshTokens.RevokeByRawTokenAsync(request.RefreshToken);
        // Always 200 — revoking an unknown/expired token is a no-op, not an error.
        return Results.Ok(new { success = true });
    }
}
