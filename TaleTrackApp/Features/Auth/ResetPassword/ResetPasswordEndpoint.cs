using TaleTrackApp.OpenApi;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.ResetPassword;

/// <summary>
/// <c>POST /api/auth/reset-password</c>: sets a new password from the token of a reset link and ends
/// every session of the account. Anonymous: the token is the credential. Returns 400 with code
/// <c>invalid_or_expired</c> if the token is unknown, used or expired.
/// </summary>
public static class ResetPasswordEndpoint
{
    /// <summary>Registers the endpoint on the <c>/api</c> route group.</summary>
    /// <param name="group">The <c>/api</c> group.</param>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/reset-password", HandleAsync)
            .WithName("ResetPassword")
            .WithTags("Auth")
            .WithSummary("Set a new password from a reset link")
            .WithDescription("The token from the emailed link is single-use and valid for 1 hour. Also ends every session of the account, so all devices must sign in again.")
            .Responds<ApiResult>("Password updated.")
            .RespondsBadRequest("Validation failed, or the code is `invalid_or_expired` (the token was already used or has expired).")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    /// <summary>Set a new password from a reset link</summary>
    private static async Task<IResult> HandleAsync(
        ResetPasswordRequest request,
        AuthActionTokenService tokens)
    {
        if (!await tokens.ResetPasswordAsync(request.Token, request.Password))
            return Results.BadRequest(new { code = "invalid_or_expired", message = "This reset link is invalid or has expired." });

        return Results.Ok(new { success = true, code = "ok", message = "Password updated" });
    }
}
