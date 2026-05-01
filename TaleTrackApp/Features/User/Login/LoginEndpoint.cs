using TaleTrackApp.Features.User;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.User.Login;

public static class LoginEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/login", HandleAsync)
            .WithName("Login")
            .WithDescription("Inicia sesión con email y contraseña")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(LoginRequest request, UserService userService, JwtService jwtService, ILogger<LoginRequest> logger)
    {
        // Process
        var user = await userService.GetByEmailAsync(request.Email);
        
        if (user == null || !userService.VerifyPassword(request.Password, user.PasswordHash))
        {
            logger.LogWarning($"Failed login attempt for email: {request.Email}");
            return Results.Json(new { message = "Invalid email or password." }, statusCode: 401);
        }

        // Generate JWT token
        var token = jwtService.GenerateToken(user.Id, user.Email, user.Username);

        logger.LogInformation($"User {user.Username} logged in successfully");

        // Response
        var response = new LoginResponse
        {
            Success = true,
            Message = "Login exitoso",
            Token = token
        };

        return Results.Ok(response);
    }
}
