using TaleTrackApp.Security;
using TaleTrackApp.Features.User;

namespace TaleTrackApp.Features.Auth.RequestCode;

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
        var issued = await userService.IssueEmailCodeAsync(request.Email);
        if (issued is not var (user, code))
            return Results.Ok(new { success = true, message = "If the email exists, a verification code has been sent" });

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
