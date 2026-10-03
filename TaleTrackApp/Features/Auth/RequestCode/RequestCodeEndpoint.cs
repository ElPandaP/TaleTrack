using TaleTrackApp.OpenApi;
using TaleTrackApp.Security;
using TaleTrackApp.Features.User;

namespace TaleTrackApp.Features.Auth.RequestCode;

/// <summary>
/// <c>POST /api/auth/request-code</c>: emails a one-time sign-in code, redeemed with
/// <see cref="VerifyCode.VerifyCodeEndpoint"/>. Anonymous. The answer is the same whether or not
/// the account exists; returns 500 if the email cannot be sent.
/// </summary>
public static class RequestCodeEndpoint
{
    /// <summary>Registers the endpoint on the <c>/api</c> route group.</summary>
    /// <param name="group">The <c>/api</c> group.</param>
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

    /// <summary>Email a one-time sign-in code</summary>
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
            logger.LogError(ex, "Failed to send verification email to user {UserId}", user.Id);
            return Results.Problem("Failed to send the email");
        }

        return Results.Ok(new { success = true, message = "If the email exists, a verification code has been sent" });
    }
}
