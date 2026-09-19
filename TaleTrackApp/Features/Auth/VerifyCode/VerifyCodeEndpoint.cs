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
        var user = await userService.GetByEmailAsync(request.Email);
        if (user == null || user.EmailCode == null || user.EmailCodeExpiry == null)
            return Results.Unauthorized();

        if (user.EmailCodeExpiry < DateTime.UtcNow)
        {
            logger.LogWarning("Expired email code attempt for {Email}", request.Email);
            return Results.Unauthorized();
        }

        if (user.EmailCode != request.Code)
        {
            logger.LogWarning("Invalid email code attempt for {Email}", request.Email);
            return Results.Unauthorized();
        }

        await userService.ClearEmailCodeAsync(user.Id);

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
