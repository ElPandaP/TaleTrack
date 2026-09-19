using TaleTrackApp.Security;
using TaleTrackApp.Services;

namespace TaleTrackApp.Features.Auth.RequestPasswordReset;

public static class RequestPasswordResetEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/request-password-reset", HandleAsync)
            .WithName("RequestPasswordReset")
            .WithDescription("Emails a password-reset link if the account exists and has a password")
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
