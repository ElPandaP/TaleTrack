using TaleTrackApp.OpenApi;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.Refresh;

public static class RefreshEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/refresh", HandleAsync)
            .WithName("RefreshToken")
            .WithTags("Auth")
            .WithSummary("Exchange a refresh token for new tokens")
            .WithDescription("The refresh token is rotated: store the new one and discard the old one. A just-rotated token keeps returning the same replacement for 60 seconds, so two callers refreshing at once do not invalidate each other.")
            .Responds<RefreshResponse>("New access and refresh tokens.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .Responds<ApiError>(StatusCodes.Status401Unauthorized, "The refresh token is unknown, revoked or expired. The user must sign in again.")
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
