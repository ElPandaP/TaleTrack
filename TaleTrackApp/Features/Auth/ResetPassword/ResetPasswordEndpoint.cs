using TaleTrackApp.OpenApi;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.ResetPassword;

public static class ResetPasswordEndpoint
{
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

    private static async Task<IResult> HandleAsync(
        ResetPasswordRequest request,
        AuthActionTokenService tokens)
    {
        if (!await tokens.ResetPasswordAsync(request.Token, request.Password))
            return Results.BadRequest(new { code = "invalid_or_expired", message = "This reset link is invalid or has expired." });

        return Results.Ok(new { success = true, code = "ok", message = "Password updated" });
    }
}
