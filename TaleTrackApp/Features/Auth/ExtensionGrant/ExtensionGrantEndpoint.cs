using System.Security.Claims;
using TaleTrackApp.Auth;
using TaleTrackApp.Features.User;

namespace TaleTrackApp.Features.Auth.ExtensionGrant;

/// <summary>
/// Called by the web app's /extension-auth page (with the user's web JWT) to hand the
/// browser extension its own access + refresh token pair.
/// </summary>
public static class ExtensionGrantEndpoint
{
    public const string DeviceName = "Netflix extension";

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/extension-grant", HandleAsync)
            .WithName("ExtensionGrant")
            .WithDescription("Issues an access + refresh token pair for the browser extension")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        UserService userService,
        RefreshTokenService refreshTokens,
        JwtService jwtService,
        ClaimsPrincipal principal,
        ILoggerFactory loggerFactory)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Results.Unauthorized();

        var user = await userService.GetByIdAsync(userId);
        if (user is null) return Results.Unauthorized();

        var token = jwtService.GenerateToken(user.Id, user.Email, user.Username);
        var refreshToken = await refreshTokens.IssueAsync(user.Id, DeviceName);

        loggerFactory.CreateLogger(nameof(ExtensionGrantEndpoint))
            .LogInformation("Extension grant issued for user {UserId}", user.Id);

        return Results.Ok(new
        {
            success = true,
            token,
            refreshToken,
            expiresIn = jwtService.ExpirationMinutes * 60,
        });
    }
}
