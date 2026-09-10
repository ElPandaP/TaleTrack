using TaleTrackApp.Features.User;
using TaleTrackApp.Features.Auth;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.User.Register;

public static class RegisterEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/register", HandleAsync)
            .WithName("Register")
            .WithDescription("Register a new user (only with internal API key)")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.InternalOnly);
    }

    private static async Task<IResult> HandleAsync(
        RegisterRequest request,
        UserService userService,
        IServiceScopeFactory scopeFactory,
        ILogger<RegisterRequest> logger)
    {
        if (await userService.EmailExistsAsync(request.Email))
        {
            logger.LogWarning($"Registration attempt with existing email: {request.Email}");
            return Results.BadRequest(new { code = "email_taken", message = "Email already registered" });
        }

        var user = await userService.CreateUserAsync(request.Email, request.Username, request.Password);
        WelcomeEmail.SendInBackground(scopeFactory, user.Id, user.Email, request.Locale);

        logger.LogInformation($"User {user.Username} registered successfully");
        return Results.Ok(new { success = true, message = "Registration successful" });
    }
}
