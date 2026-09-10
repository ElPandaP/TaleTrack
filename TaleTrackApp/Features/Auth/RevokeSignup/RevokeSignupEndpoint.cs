using System.ComponentModel.DataAnnotations;
using TaleTrackApp.Auth;
using TaleTrackApp.Features.User;
using TaleTrackApp.Model;

namespace TaleTrackApp.Features.Auth.RevokeSignup;

public class RevokeSignupRequest
{
    [Required(ErrorMessage = "token is required")]
    public required string Token { get; set; }
}

public static class RevokeSignupEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/revoke-signup", HandleAsync)
            .WithName("RevokeSignup")
            .WithDescription("Deletes an account created by someone who did not own the email address")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        RevokeSignupRequest request,
        AuthActionTokenService tokens,
        UserService userService,
        ILogger<RevokeSignupRequest> logger)
    {
        var user = await tokens.ConsumeAsync(request.Token, AuthActionToken.SignupRevoke);
        if (user is null)
            return Results.BadRequest(new { code = "invalid_or_expired", message = "This link is invalid or has expired." });

        await userService.DeleteUserAsync(user.Id);
        logger.LogInformation("Account {UserId} deleted via signup-revoke link", user.Id);
        return Results.Ok(new { success = true, message = "Account deleted" });
    }
}
