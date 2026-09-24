using TaleTrackApp.OpenApi;
using TaleTrackApp.Security;
using TaleTrackApp.Services;

namespace TaleTrackApp.Features.Auth.RequestPasswordReset;

public static class RequestPasswordResetEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/request-password-reset", HandleAsync)
            .WithName("RequestPasswordReset")
            .WithTags("Auth")
            .WithSummary("Email a password-reset link")
            .WithDescription("The answer is the same whether or not an account exists, and accounts without a password (Google-only) get no email. The emailed link is valid for 1 hour.")
            .Responds<ApiResult>("Accepted; an email was sent if the account exists.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    // Runs in the background so the response is instant and identical whether or not the
    // email exists (no account enumeration).
    private static IResult HandleAsync(
        RequestPasswordResetRequest request,
        BackgroundRunner background)
    {
        var (email, locale) = (request.Email, request.Locale);
        background.Run<AuthActionTokenService>("password-reset email",
            tokens => tokens.SendPasswordResetAsync(email, locale));

        return Results.Ok(new { success = true });
    }
}
