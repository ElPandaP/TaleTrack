using TaleTrackApp.Security;
using TaleTrackApp.Services;

namespace TaleTrackApp.Features.Auth.GoogleSignupComplete;

public static class GoogleSignupCompleteEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/google/complete", HandleAsync)
            .WithName("GoogleSignupComplete")
            .WithDescription("Finishes a Google sign-up once the user has picked a username")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        GoogleSignupCompleteRequest request,
        GoogleAuthService google,
        SessionService sessions,
        BackgroundRunner background,
        ILogger<GoogleSignupCompleteRequest> logger)
    {
        switch (await google.CompleteSignupAsync(request.PendingToken, request.Username))
        {
            case GoogleSignupResult.InvalidToken:
                return Results.BadRequest(new { code = "invalid_or_expired", message = "This sign-up link is invalid or has expired." });
            case GoogleSignupResult.UsernameTaken:
                return Results.BadRequest(new { code = "username_taken", message = "Username already taken" });
            case GoogleSignupResult.Completed done:
                if (done.Created)
                    background.Run<AuthActionTokenService>("welcome email",
                        tokens => tokens.SendWelcomeAsync(done.User.Id, done.User.Email, request.Locale));

                var session = await sessions.StartAsync(done.User, "Web");
                logger.LogInformation("User {Username} completed Google sign-up", done.User.Username);

                return Results.Ok(new GoogleSignupCompleteResponse
                {
                    Success = true,
                    Message = "Registration successful",
                    Token = session.AccessToken,
                    RefreshToken = session.RefreshToken,
                    ExpiresIn = session.ExpiresIn,
                });
            default:
                throw new InvalidOperationException("Unhandled Google sign-up result");
        }
    }
}
