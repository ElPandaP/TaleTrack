using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Security;
using TaleTrackApp.Features.User;

namespace TaleTrackApp.Features.Auth.ExtensionGrant;

/// <summary>
/// <c>POST /api/auth/extension-grant</c>: called by the web app's <c>/extension-auth</c> page, with
/// the user's own access token, to give a browser extension an independent session (its own
/// access and refresh tokens, listed in the sessions list under the request's device name, or
/// "Netflix extension" without one). Requires a valid JWT; returns 401 if the token's user no
/// longer exists.
/// </summary>
public static class ExtensionGrantEndpoint
{
    /// <summary>Session label used when the request names no device: the Netflix extension's.</summary>
    public const string DefaultDeviceName = "Netflix extension";

    /// <summary>Registers the endpoint on the <c>/api</c> route group.</summary>
    /// <param name="group">The <c>/api</c> group.</param>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/extension-grant", HandleAsync)
            .WithName("ExtensionGrant")
            .WithTags("Auth")
            .WithSummary("Issue tokens for the browser extension")
            .WithDescription("Called by the web app with the user's own token to hand a browser extension an independent session. `device` labels it in the sessions list and defaults to `Netflix extension`; the body can be omitted.")
            .Responds<ExtensionGrantResponse>("New access and refresh tokens for the extension.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Issue tokens for the browser extension</summary>
    private static async Task<IResult> HandleAsync(
        ExtensionGrantRequest? request,
        UserService userService,
        SessionService sessions,
        ClaimsPrincipal principal,
        ILoggerFactory loggerFactory)
    {
        if (!principal.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var user = await userService.GetByIdAsync(userId);
        if (user is null) return Results.Unauthorized();

        var device = string.IsNullOrWhiteSpace(request?.Device) ? DefaultDeviceName : request.Device.Trim();
        var session = await sessions.StartAsync(user, device);

        loggerFactory.CreateLogger(nameof(ExtensionGrantEndpoint))
            .LogInformation("Extension grant issued for user {UserId} ({Device})", user.Id, device);

        return Results.Ok(new ExtensionGrantResponse
        {
            Success = true,
            Token = session.AccessToken,
            RefreshToken = session.RefreshToken,
            ExpiresIn = session.ExpiresIn,
        });
    }
}
