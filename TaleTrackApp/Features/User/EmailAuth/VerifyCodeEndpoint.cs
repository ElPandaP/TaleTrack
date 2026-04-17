using System.ComponentModel.DataAnnotations;
using TaleTrackApp.Auth;
using TaleTrackApp.Features.User;

namespace TaleTrackApp.Features.User.EmailAuth;

public class VerifyCodeRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public required string Code { get; set; }
}

public static class VerifyCodeEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/verify-code", HandleAsync)
            .WithName("VerifyEmailCode")
            .WithDescription("Verifica el código de email y devuelve un JWT")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        VerifyCodeRequest request,
        UserService userService,
        JwtService jwtService,
        ILogger<VerifyCodeRequest> logger)
    {
        var user = await userService.GetByEmailAsync(request.Email);
        if (user == null || user.EmailCode == null || user.EmailCodeExpiry == null)
            return Results.Unauthorized();

        if (user.EmailCodeExpiry < DateTime.UtcNow)
        {
            logger.LogWarning("Expired email code attempt for {Email}", request.Email);
            return Results.Unauthorized();
        }

        if (user.EmailCode != request.Code)
        {
            logger.LogWarning("Invalid email code attempt for {Email}", request.Email);
            return Results.Unauthorized();
        }

        await userService.ClearEmailCodeAsync(user.Id);

        var token = jwtService.GenerateToken(user.Id, user.Email, user.Username);
        logger.LogInformation("User {Username} authenticated via email code", user.Username);

        return Results.Ok(new { success = true, message = "Verificación exitosa", token });
    }
}
