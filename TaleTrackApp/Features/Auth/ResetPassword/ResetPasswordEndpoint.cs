using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Auth.ResetPassword;

public static class ResetPasswordEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/reset-password", HandleAsync)
            .WithName("ResetPassword")
            .WithDescription("Sets a new password from a valid reset token and ends all sessions")
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
