using System.Security.Claims;
using TaleTrackApp.Security;
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
        SessionService sessions,
        ClaimsPrincipal principal,
        ILoggerFactory loggerFactory)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        var user = await userService.GetByIdAsync(userId);
        if (user is null) return Results.Unauthorized();

        var session = await sessions.StartAsync(user, DeviceName);

        loggerFactory.CreateLogger(nameof(ExtensionGrantEndpoint))
            .LogInformation("Extension grant issued for user {UserId}", user.Id);

        return Results.Ok(new ExtensionGrantResponse
        {
            Success = true,
            Token = session.AccessToken,
            RefreshToken = session.RefreshToken,
            ExpiresIn = session.ExpiresIn,
        });
    }
}
