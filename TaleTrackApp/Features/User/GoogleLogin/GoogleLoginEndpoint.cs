using Google.Apis.Auth;
using TaleTrackApp.Auth;
using TaleTrackApp.Features.Auth;
using TaleTrackApp.Features.User;

namespace TaleTrackApp.Features.User.GoogleLogin;

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
        UserService userService,
        JwtService jwtService,
        RefreshTokenService refreshTokens,
        IServiceScopeFactory scopeFactory,
        ILogger<GoogleLoginRequest> logger)
    {
        var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
        if (string.IsNullOrEmpty(clientId))
            return Results.Problem("Google OAuth not configured on the server");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId]
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning("Invalid Google token: {Message}", ex.Message);
            return Results.Unauthorized();
        }

        var googleId = payload.Subject;
        var email = payload.Email;
        var name = !string.IsNullOrEmpty(payload.Name) ? payload.Name : email.Split('@')[0];

        var user = await userService.GetByGoogleIdAsync(googleId);

        if (user == null)
        {
            user = await userService.GetByEmailAsync(email);
            if (user != null)
            {
                await userService.LinkGoogleIdAsync(user.Id, googleId);
            }
            else
            {
                user = await userService.CreateGoogleUserAsync(email, name, googleId);
                WelcomeEmail.SendInBackground(scopeFactory, user.Id, user.Email, request.Locale);
            }
        }

        var token = jwtService.GenerateToken(user.Id, user.Email, user.Username);
        var refreshToken = await refreshTokens.IssueAsync(user.Id, "Web");
        logger.LogInformation("User {Username} logged in via Google", user.Username);

        return Results.Ok(new
        {
            success = true,
            message = "Login successful",
            token,
            refreshToken,
            expiresIn = jwtService.ExpirationMinutes * 60,
        });
    }
}
