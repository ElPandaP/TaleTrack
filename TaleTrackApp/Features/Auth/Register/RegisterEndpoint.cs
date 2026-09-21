using TaleTrackApp.Features.User;
using TaleTrackApp.Security;
using TaleTrackApp.Services;

namespace TaleTrackApp.Features.Auth.Register;

public static class RegisterEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/register", HandleAsync)
            .WithName("Register")
            .WithDescription("Register a new user")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        RegisterRequest request,
        UserService userService,
        BackgroundRunner background,
        ILogger<RegisterRequest> logger)
    {
        var (result, user) = await userService.RegisterAsync(request.Email, request.Username, request.Password);
        switch (result)
        {
            case RegisterResult.EmailTaken:
                return Results.BadRequest(new { code = "email_taken", message = "Email already registered" });
            case RegisterResult.UsernameTaken:
                return Results.BadRequest(new { code = "username_taken", message = "Username already taken" });
        }

        background.Run<AuthActionTokenService>("welcome email",
            tokens => tokens.SendWelcomeAsync(user!.Id, user.Email, request.Locale));

        logger.LogInformation("User {Username} registered successfully", user!.Username);
        return Results.Ok(new { success = true, message = "Registration successful" });
    }
}
