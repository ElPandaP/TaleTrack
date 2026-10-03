using TaleTrackApp.OpenApi;
using TaleTrackApp.Features.User;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.Login;

/// <summary>
/// <c>POST /api/login</c>: signs in with email and password and starts a "Web" session. Anonymous.
/// Returns 401 with code <c>invalid_credentials</c> when the email or password is wrong.
/// </summary>
public static class LoginEndpoint
{
    /// <summary>Registers the endpoint on the <c>/api</c> route group.</summary>
    /// <param name="group">The <c>/api</c> group.</param>
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

    /// <summary>Sign in with email and password</summary>
    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        UserService userService,
        SessionService sessions,
        ILogger<LoginRequest> logger)
    {
        var user = await userService.AuthenticateAsync(request.Email, request.Password);

        if (user == null)
        {
            return Results.Json(new ApiError { Success = false, Code = "invalid_credentials", Message = "Invalid email or password." }, statusCode: 401);
        }

        var session = await sessions.StartAsync(user, "Web");

        logger.LogInformation("User {Username} logged in", user.Username);

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
