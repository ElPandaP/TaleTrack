using TaleTrackApp.OpenApi;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.GoogleLogin;

/// <summary>
/// <c>POST /api/auth/google</c>: signs in with a Google id token. Anonymous. An existing account (or
/// one with the same email, which gets linked) receives its tokens; an unknown Google account gets
/// <c>needsUsername</c> and a pending token to finish the sign-up with
/// <see cref="GoogleSignupComplete.GoogleSignupCompleteEndpoint"/>. Returns 401 with code
/// <c>invalid_google_token</c> for an invalid id token and 500 when Google sign-in is not configured.
/// </summary>
public static class GoogleLoginEndpoint
{
    /// <summary>Registers the endpoint on the <c>/api</c> route group.</summary>
    /// <param name="group">The <c>/api</c> group.</param>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/google", HandleAsync)
            .WithName("GoogleLogin")
            .WithTags("Auth")
            .WithSummary("Sign in with a Google ID token")
            .WithDescription("Two outcomes. An existing account (or one whose email matches, which gets linked) receives its tokens. An unknown Google account receives `needsUsername: true` plus a `pendingToken` to send, with the chosen username, to `POST /api/auth/google/complete`.")
            .Responds<GoogleLoginResponse>("Signed in, or the sign-up must be completed with a username.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .Responds<ApiError>(StatusCodes.Status401Unauthorized, "The Google ID token is invalid (code `invalid_google_token`).")
            .Responds(StatusCodes.Status500InternalServerError, "Google sign-in is not configured on the server.")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    /// <summary>Sign in with a Google ID token</summary>
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
                return Results.Json(new ApiError { Success = false, Code = "invalid_google_token", Message = "The Google ID token is invalid." }, statusCode: 401);

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
