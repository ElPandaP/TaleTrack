using TaleTrackApp.Features.User;
using TaleTrackApp.Features.Auth;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.User.Login;

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
        JwtService jwtService,
        RefreshTokenService refreshTokens,
        ILogger<LoginRequest> logger)
    {
        var user = await userService.GetByEmailAsync(request.Email);

        if (user == null || !userService.VerifyPassword(request.Password, user.PasswordHash))
        {
            logger.LogWarning($"Failed login attempt for email: {request.Email}");
            return Results.Json(new { message = "Email o contraseña incorrectos." }, statusCode: 401);
        }

        var token = jwtService.GenerateToken(user.Id, user.Email, user.Username);
        var refreshToken = await refreshTokens.IssueAsync(user.Id, "Web");

        logger.LogInformation($"User {user.Username} logged in successfully");

        var response = new LoginResponse
        {
            Success = true,
            Message = "Login exitoso",
            Token = token,
            RefreshToken = refreshToken,
            ExpiresIn = jwtService.ExpirationMinutes * 60,
        };

        return Results.Ok(response);
    }
}
