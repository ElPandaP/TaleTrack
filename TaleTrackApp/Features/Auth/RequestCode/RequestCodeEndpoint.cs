using TaleTrackApp.OpenApi;
using TaleTrackApp.Security;
using TaleTrackApp.Features.User;

namespace TaleTrackApp.Features.Auth.RequestCode;

public static class RequestCodeEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/request-code", HandleAsync)
            .WithName("RequestEmailCode")
            .WithTags("Auth")
            .WithSummary("Email a one-time sign-in code")
            .WithDescription("Passwordless sign-in (used by the KOReader plugin). The answer is the same whether or not an account exists for the email, so it cannot be used to discover accounts. Redeem the code, valid for 10 minutes, with `POST /api/auth/verify-code`.")
            .Responds<ApiResult>("Accepted; a code was sent if the account exists.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .Responds(StatusCodes.Status500InternalServerError, "The email could not be sent.")
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
