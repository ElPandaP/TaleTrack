using System.ComponentModel.DataAnnotations;
using TaleTrackApp.Auth;
using TaleTrackApp.Features.User;

namespace TaleTrackApp.Features.User.EmailAuth;

public class RequestCodeRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email must be valid")]
    public required string Email { get; set; }

    /// <summary>Requester UI locale ("es"/"en"); the verification email is sent in it.</summary>
    [StringLength(10)]
    public string? Locale { get; set; }
}

public static class RequestCodeEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/request-code", HandleAsync)
            .WithName("RequestEmailCode")
            .WithDescription("Sends a verification code to the given email")
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
            return Results.Ok(new { success = true, message = "If the email exists, a verification code has been sent" });

        var code = Random.Shared.Next(100000, 999999).ToString();
        await userService.SetEmailCodeAsync(user.Id, code);

        try
        {
            await emailService.SendVerificationCodeAsync(user.Email, code, request.Locale);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
            return Results.Problem("Failed to send the email");
        }

        return Results.Ok(new { success = true, message = "If the email exists, a verification code has been sent" });
    }
}
