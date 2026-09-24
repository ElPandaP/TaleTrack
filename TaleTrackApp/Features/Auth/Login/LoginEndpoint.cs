using TaleTrackApp.OpenApi;
using TaleTrackApp.Features.User;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.Login;

public static class LoginEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/login", HandleAsync)
            .WithName("Login")
            .WithTags("Auth")
            .WithSummary("Sign in with email and password")
            .Responds<LoginResponse>("Signed in: access token, refresh token and access-token lifetime.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .Responds<ApiError>(StatusCodes.Status401Unauthorized, "Wrong email or password (code `invalid_credentials`).")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        UserService userService,
        SessionService sessions,
        ILogger<LoginRequest> logger)
    {
        var user = await userService.AuthenticateAsync(request.Email, request.Password);

        if (user == null)
        {
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
