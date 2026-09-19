using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.GoogleLogin;

public static class GoogleLoginEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/google", HandleAsync)
            .WithName("GoogleLogin")
            .WithDescription("Signs in with Google OAuth")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        GoogleLoginRequest request,
        GoogleAuthService google,
        SessionService sessions,
        ILogger<GoogleLoginRequest> logger)
    {
        switch (await google.LoginAsync(request.IdToken))
        {
            case GoogleLoginResult.NotConfigured:
                return Results.Problem("Google OAuth not configured on the server");

            case GoogleLoginResult.InvalidToken:
                return Results.Unauthorized();

            case GoogleLoginResult.NewUser newUser:
                return Results.Ok(new GoogleLoginResponse
                {
                    Success = true,
                    NeedsUsername = true,
                    PendingToken = newUser.PendingToken,
                    Email = newUser.Email,
                    SuggestedUsername = newUser.SuggestedUsername,
                });

            case GoogleLoginResult.ExistingUser existing:
                var session = await sessions.StartAsync(existing.User, "Web");
                logger.LogInformation("User {Username} logged in via Google", existing.User.Username);

                return Results.Ok(new GoogleLoginResponse
                {
                    Success = true,
                    Message = "Login successful",
                    Token = session.AccessToken,
                    RefreshToken = session.RefreshToken,
                    ExpiresIn = session.ExpiresIn,
                    LinkedExistingAccount = existing.LinkedExistingAccount,
                });

            default:
                throw new InvalidOperationException("Unhandled Google login result");
        }
    }
}
