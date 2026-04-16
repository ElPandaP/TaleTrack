using TaleTrackApp.Features.User;
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
        ILogger<RegisterRequest> logger)
    {
        // Process
        if (await userService.EmailExistsAsync(request.Email))
        {
            logger.LogWarning($"Registration attempt with existing email: {request.Email}");
            return Results.BadRequest(new { message = "El email ya está registrado" });
        }

        var user = await userService.CreateUserAsync(request.Email, request.Username, request.Password);

        logger.LogInformation($"User {user.Username} registered successfully");
        return Results.Ok(new { success = true, message = "Registro exitoso" });
    }
}
