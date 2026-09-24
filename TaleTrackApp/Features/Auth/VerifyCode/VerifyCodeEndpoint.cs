using TaleTrackApp.OpenApi;
using TaleTrackApp.Security;
using TaleTrackApp.Features.User;

namespace TaleTrackApp.Features.Auth.VerifyCode;

public static class VerifyCodeEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/verify-code", HandleAsync)
            .WithName("VerifyEmailCode")
            .WithTags("Auth")
            .WithSummary("Sign in with an emailed code")
            .WithDescription("Redeems the code from `POST /api/auth/request-code` for a session. `device` labels the session in the sessions list and defaults to `KOReader`. The six-digit code is valid for 10 minutes and works once; five wrong guesses discard it and a new one must be requested.")
            .Responds<VerifyCodeResponse>("Signed in.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .Responds(StatusCodes.Status401Unauthorized, "Wrong, expired or discarded code (five wrong guesses).")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        VerifyCodeRequest request,
        UserService userService,
        SessionService sessions,
        ILogger<VerifyCodeRequest> logger)
    {
        var user = await userService.VerifyEmailCodeAsync(request.Email, request.Code);
        if (user == null)
            return Results.Unauthorized();

        var device = string.IsNullOrWhiteSpace(request.Device) ? "KOReader" : request.Device;
        var session = await sessions.StartAsync(user, device);
        logger.LogInformation("User {Username} authenticated via email code", user.Username);

        return Results.Ok(new VerifyCodeResponse
        {
            Success = true,
            Message = "Verification successful",
            Token = session.AccessToken,
            RefreshToken = session.RefreshToken,
            ExpiresIn = session.ExpiresIn,
        });
    }
}
