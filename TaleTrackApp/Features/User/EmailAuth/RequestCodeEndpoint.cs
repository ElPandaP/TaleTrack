using System.ComponentModel.DataAnnotations;
using TaleTrackApp.Auth;
using TaleTrackApp.Features.User;

namespace TaleTrackApp.Features.User.EmailAuth;

public class RequestCodeRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
}

public static class RequestCodeEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/request-code", HandleAsync)
            .WithName("RequestEmailCode")
            .WithDescription("Envía un código de verificación al email indicado")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        RequestCodeRequest request,
        UserService userService,
        EmailService emailService,
        ILogger<RequestCodeRequest> logger)
    {
        var user = await userService.GetByEmailAsync(request.Email);
        if (user == null)
            return Results.Ok(new { success = true, message = "Si el email existe, recibirás un código" });

        var code = Random.Shared.Next(100000, 999999).ToString();
        await userService.SetEmailCodeAsync(user.Id, code);

        try
        {
            await emailService.SendVerificationCodeAsync(user.Email, code);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
            return Results.Problem("Error al enviar el email");
        }

        return Results.Ok(new { success = true, message = "Si el email existe, recibirás un código" });
    }
}
