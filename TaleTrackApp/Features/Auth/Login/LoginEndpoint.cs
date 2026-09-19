using TaleTrackApp.Features.User;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.Login;

public static class LoginEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/login", HandleAsync)
            .WithName("Login")
            .WithDescription("Signs in with email and password")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        UserService userService,
        SessionService sessions,
        ILogger<LoginRequest> logger)
    {
        var user = await userService.GetByEmailAsync(request.Email);

        if (user == null || !userService.VerifyPassword(request.Password, user))
        {
            logger.LogWarning($"Failed login attempt for email: {request.Email}");
            return Results.Json(new { code = "invalid_credentials", message = "Invalid email or password." }, statusCode: 401);
        }

        var session = await sessions.StartAsync(user, "Web");

        logger.LogInformation($"User {user.Username} logged in successfully");

        return Results.Ok(new LoginResponse
        {
            Success = true,
            Message = "Login successful",
            Token = session.AccessToken,
            RefreshToken = session.RefreshToken,
            ExpiresIn = session.ExpiresIn,
        });
    }
}
