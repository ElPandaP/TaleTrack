using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.Refresh;

public static class RefreshEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/refresh", HandleAsync)
            .WithName("RefreshToken")
            .WithDescription("Exchanges a valid refresh token for a new access + refresh token pair")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        RefreshRequest request,
        SessionService sessions)
    {
        var session = await sessions.RefreshAsync(request.RefreshToken);
        if (session is null)
            return Results.Json(new { success = false, message = "Invalid or expired refresh token." }, statusCode: 401);

        return Results.Ok(new RefreshResponse
        {
            Success = true,
            Token = session.AccessToken,
            RefreshToken = session.RefreshToken,
            ExpiresIn = session.ExpiresIn,
        });
    }
}
