using TaleTrackApp.OpenApi;
using TaleTrackApp.Security;
using TaleTrackApp.Services;

namespace TaleTrackApp.Features.Auth.GoogleSignupComplete;

/// <summary>
/// <c>POST /api/auth/google/complete</c>: creates the account for a pending Google sign-up with the
/// chosen username, sends the welcome email in the background and signs the user in. Anonymous:
/// the pending token is the credential. Returns 400 with code <c>invalid_or_expired</c> or
/// <c>username_taken</c>.
/// </summary>
public static class GoogleSignupCompleteEndpoint
{
    /// <summary>Registers the endpoint on the <c>/api</c> route group.</summary>
    /// <param name="group">The <c>/api</c> group.</param>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/google/complete", HandleAsync)
            .WithName("GoogleSignupComplete")
            .WithTags("Auth")
            .WithSummary("Finish a Google sign-up")
            .WithDescription("Creates the account for the `pendingToken` returned by `POST /api/auth/google` with the chosen username, and signs the user in.")
            .Responds<GoogleSignupCompleteResponse>("Account created and signed in.")
            .RespondsBadRequest("Validation failed, or the code is `invalid_or_expired` (pending token) / `username_taken`.")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    /// <summary>Finish a Google sign-up</summary>
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
