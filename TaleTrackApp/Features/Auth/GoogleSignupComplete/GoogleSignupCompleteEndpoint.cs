using TaleTrackApp.Security;
using TaleTrackApp.Services;
using TaleTrackApp.Features.User;

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
        GoogleSignupTokenService signupTokens,
        UserService userService,
        SessionService sessions,
        BackgroundRunner background,
        ILogger<GoogleSignupCompleteRequest> logger)
    {
        var identity = signupTokens.Validate(request.PendingToken);
        if (identity == null)
            return Results.BadRequest(new { code = "invalid_or_expired", message = "This sign-up link is invalid or has expired." });

        var (googleId, email, _) = identity.Value;

        // Defensive re-check: a double-submit (or a normal signup / another Google login for
        // the same person in the meantime) may have created the account already — log in
        // instead of erroring.
        var user = await userService.GetByGoogleIdAsync(googleId) ?? await userService.GetByEmailAsync(email);

        if (user == null)
        {
            if (await userService.UsernameExistsAsync(request.Username))
                return Results.BadRequest(new { code = "username_taken", message = "Username already taken" });

            user = await userService.CreateGoogleUserAsync(email, request.Username, googleId);
            background.Run<AuthActionTokenService>("welcome email",
                tokens => tokens.SendWelcomeAsync(user.Id, user.Email, request.Locale));
        }
        else if (user.GoogleId == null)
        {
            await userService.LinkGoogleIdAsync(user.Id, googleId);
        }

        var session = await sessions.StartAsync(user, "Web");
        logger.LogInformation("User {Username} completed Google sign-up", user.Username);

        return Results.Ok(new GoogleSignupCompleteResponse
        {
            Success = true,
            Message = "Registration successful",
            Token = session.AccessToken,
            RefreshToken = session.RefreshToken,
            ExpiresIn = session.ExpiresIn,
        });
    }
}
