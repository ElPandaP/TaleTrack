using System.ComponentModel.DataAnnotations;
using TaleTrackApp.Auth;
using TaleTrackApp.Features.User;
using TaleTrackApp.Model;

namespace TaleTrackApp.Features.Auth.PasswordReset;

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "token is required")]
    public required string Token { get; set; }

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
    public required string Password { get; set; }
}

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
        RefreshTokenService refreshTokens,
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
