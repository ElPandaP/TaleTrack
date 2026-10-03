using TaleTrackApp.OpenApi;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.Refresh;

/// <summary>
/// <c>POST /api/auth/refresh</c>: exchanges a refresh token for a new access token and a new
/// (rotated) refresh token. Anonymous: the refresh token is the credential. Returns 401 with code
/// <c>invalid_refresh_token</c> when the token is unknown, revoked or expired, and the user must
/// sign in again.
/// </summary>
public static class RefreshEndpoint
{
    /// <summary>Registers the endpoint on the <c>/api</c> route group.</summary>
    /// <param name="group">The <c>/api</c> group.</param>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/refresh", HandleAsync)
            .WithName("RefreshToken")
            .WithTags("Auth")
            .WithSummary("Exchange a refresh token for new tokens")
            .WithDescription("The refresh token is rotated: store the new one and discard the old one. A just-rotated token keeps returning the same replacement for 60 seconds, so two callers refreshing at once do not invalidate each other.")
            .Responds<RefreshResponse>("New access and refresh tokens.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .Responds<ApiError>(StatusCodes.Status401Unauthorized, "The refresh token is unknown, revoked or expired (code `invalid_refresh_token`). The user must sign in again.")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    /// <summary>Exchange a refresh token for new tokens</summary>
    private static async Task<IResult> HandleAsync(
        RefreshRequest request,
        SessionService sessions)
    {
        var session = await sessions.RefreshAsync(request.RefreshToken);
        if (session is null)
            return Results.Json(new ApiError { Success = false, Code = "invalid_refresh_token", Message = "Invalid or expired refresh token." }, statusCode: 401);

        return Results.Ok(new RefreshResponse
        {
            Success = true,
            Token = session.AccessToken,
            RefreshToken = session.RefreshToken,
            ExpiresIn = session.ExpiresIn,
        });
    }
}
