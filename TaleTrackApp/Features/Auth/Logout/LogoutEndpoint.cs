using TaleTrackApp.OpenApi;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.Logout;

/// <summary>
/// <c>POST /api/auth/logout</c>: ends one session by revoking its refresh token. Used by clients to
/// sign themselves out (e.g. the browser extension). Anonymous: the refresh token is the credential.
/// Always answers 200, also when the token is unknown or already expired.
/// </summary>
public static class LogoutEndpoint
{
    /// <summary>Registers the endpoint on the <c>/api</c> route group.</summary>
    /// <param name="group">The <c>/api</c> group.</param>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/logout", HandleAsync)
            .WithName("Logout")
            .WithTags("Auth")
            .WithSummary("Sign out one session")
            .WithDescription("Revokes the given refresh token. It is a no-op, still answering 200, when the token is unknown or already expired.")
            .Responds<ApiResult>("The session is ended.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    /// <summary>Sign out one session</summary>
    private static async Task<IResult> HandleAsync(
        LogoutRequest request,
        SessionService refreshTokens)
    {
        await refreshTokens.RevokeByRawTokenAsync(request.RefreshToken);
        return Results.Ok(new { success = true });
    }
}
