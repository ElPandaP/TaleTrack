using TaleTrackApp.OpenApi;
using TaleTrackApp.Security;
using TaleTrackApp.Features.User;
using TaleTrackApp.Model;

namespace TaleTrackApp.Features.Auth.ConfirmDelete;

/// <summary>
/// <c>POST /api/auth/confirm-delete</c>: deletes an account, with all its data, from the link of
/// the account-deletion confirmation email. Anonymous: the single-use token in the link is the
/// credential. Returns 400 with code <c>invalid_or_expired</c> if the token is unknown, used,
/// expired or meant for another action.
/// </summary>
public static class ConfirmDeleteEndpoint
{
    /// <summary>Registers the endpoint on the <c>/api</c> route group.</summary>
    /// <param name="group">The <c>/api</c> group.</param>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/confirm-delete", HandleAsync)
            .WithName("ConfirmDelete")
            .WithTags("Auth")
            .WithSummary("Delete the account named by an emailed link")
            .WithDescription("Anonymous on purpose: the single-use token from the confirmation email is the credential. Deletes the account with its reviews and tracking. The token is single-use and valid for 1 hour.")
            .Responds<ApiResult>("Account deleted.")
            .RespondsBadRequest("Validation failed, or the code is `invalid_or_expired` (the token is unknown, already used, expired or meant for another action).")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    /// <summary>Delete the account named by an emailed link</summary>
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
