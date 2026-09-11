using TaleTrackApp.Auth;
using TaleTrackApp.Features.Auth;
using TaleTrackApp.Features.User.GoogleLogin;

namespace TaleTrackApp.Features.User.GoogleSignupComplete;

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
        JwtService jwtService,
        RefreshTokenService refreshTokens,
        IServiceScopeFactory scopeFactory,
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
            WelcomeEmail.SendInBackground(scopeFactory, user.Id, user.Email, request.Locale);
        }
        else if (user.GoogleId == null)
        {
            await userService.LinkGoogleIdAsync(user.Id, googleId);
        }

        var token = jwtService.GenerateToken(user.Id, user.Email, user.Username);
        var refreshToken = await refreshTokens.IssueAsync(user.Id, "Web");
        logger.LogInformation("User {Username} completed Google sign-up", user.Username);

        return Results.Ok(new
        {
            success = true,
            message = "Registration successful",
            token,
            refreshToken,
            expiresIn = jwtService.ExpirationMinutes * 60,
        });
    }
}
