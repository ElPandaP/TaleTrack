using TaleTrackApp.OpenApi;
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
            .WithTags("Auth")
            .WithSummary("Create an account with email and password")
            .WithDescription("Creates the account and sends a welcome email in the background, in the given locale. It does not sign the user in: call the login endpoint afterwards.")
            .Responds<ApiResult>("The account was created.")
            .RespondsBadRequest("Validation failed, or the code is `email_taken` / `username_taken`.")
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
