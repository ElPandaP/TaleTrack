using TaleTrackApp.Security;
using TaleTrackApp.Features.User;
using TaleTrackApp.Model;

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
        AuthActionTokenService tokens,
        UserService userService,
        SessionService refreshTokens,
        ILogger<ResetPasswordRequest> logger)
    {
        var user = await tokens.ConsumeAsync(request.Token, AuthActionToken.PasswordReset);
        if (user is null)
            return Results.BadRequest(new { code = "invalid_or_expired", message = "This reset link is invalid or has expired." });

        await userService.SetPasswordAsync(user.Id, request.Password);
        await refreshTokens.RevokeAllForUserAsync(user.Id);

        logger.LogInformation("Password reset completed for user {UserId}", user.Id);
        return Results.Ok(new { success = true, code = "ok", message = "Password updated" });
    }
}
