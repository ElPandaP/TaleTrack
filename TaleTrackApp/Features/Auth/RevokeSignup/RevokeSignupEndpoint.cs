using TaleTrackApp.OpenApi;
using TaleTrackApp.Security;
using TaleTrackApp.Features.User;
using TaleTrackApp.Model;

namespace TaleTrackApp.Features.Auth.RevokeSignup;

/// <summary>
/// <c>POST /api/auth/revoke-signup</c>: deletes an account from the "I didn't sign up" link of the
/// welcome email, so the real owner of an address can remove an account someone else created with
/// it. Anonymous: the single-use token is the credential. Returns 400 with code
/// <c>invalid_or_expired</c> if the token is unknown, used, expired or meant for another action.
/// </summary>
public static class RevokeSignupEndpoint
{
    /// <summary>Registers the endpoint on the <c>/api</c> route group.</summary>
    /// <param name="group">The <c>/api</c> group.</param>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/revoke-signup", HandleAsync)
            .WithName("RevokeSignup")
            .WithTags("Auth")
            .WithSummary("Delete an account you did not create")
            .WithDescription("Linked from the welcome email: lets the real owner of an email address delete an account someone else registered with it. The token is single-use and valid for 7 days.")
            .Responds<ApiResult>("Account deleted.")
            .RespondsBadRequest("Validation failed, or the code is `invalid_or_expired` (the token is unknown, already used, expired or meant for another action).")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    /// <summary>Delete an account you did not create</summary>
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
