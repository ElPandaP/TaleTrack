using System.ComponentModel.DataAnnotations;
using TaleTrackApp.Auth;
using TaleTrackApp.Features.User;
using TaleTrackApp.Model;

namespace TaleTrackApp.Features.Auth.ConfirmDelete;

public class ConfirmDeleteRequest
{
    [Required(ErrorMessage = "token is required")]
    public required string Token { get; set; }
}

public static class ConfirmDeleteEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/confirm-delete", HandleAsync)
            .WithName("ConfirmDelete")
            .WithDescription("Deletes the account named by a valid delete-confirmation token")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        ConfirmDeleteRequest request,
        AuthActionTokenService tokens,
        UserService userService,
        ILogger<ConfirmDeleteRequest> logger)
    {
        var user = await tokens.ConsumeAsync(request.Token, AuthActionToken.DeleteAccount);
        if (user is null)
            return Results.BadRequest(new { code = "invalid_or_expired", message = "This confirmation link is invalid or has expired." });

        await userService.DeleteUserAsync(user.Id);
        logger.LogInformation("Account {UserId} deleted via email confirmation", user.Id);
        return Results.Ok(new { success = true, message = "Account deleted" });
    }
}
