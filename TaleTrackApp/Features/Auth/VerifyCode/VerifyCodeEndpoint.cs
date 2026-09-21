using TaleTrackApp.Security;
using TaleTrackApp.Features.User;

namespace TaleTrackApp.Features.Auth.VerifyCode;

public static class VerifyCodeEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/verify-code", HandleAsync)
            .WithName("VerifyEmailCode")
            .WithDescription("Verifies the email code and returns a JWT")
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
