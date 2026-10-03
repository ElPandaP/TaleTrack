using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Security;
using TaleTrackApp.Features.User;
using TaleTrackApp.Model;

namespace TaleTrackApp.Features.Auth.RequestDeletion;

/// <summary>
/// <c>POST /api/auth/request-account-deletion</c>: emails the caller a link that confirms the
/// deletion of their account (<see cref="ConfirmDelete.ConfirmDeleteEndpoint"/>). Nothing is deleted
/// yet. Requires a valid JWT.
/// </summary>
public static class RequestAccountDeletionEndpoint
{
    /// <summary>Registers the endpoint on the <c>/api</c> route group.</summary>
    /// <param name="group">The <c>/api</c> group.</param>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/request-account-deletion", HandleAsync)
            .WithName("RequestAccountDeletion")
            .WithTags("Auth")
            .WithSummary("Email an account-deletion confirmation link")
            .WithDescription("Nothing is deleted until the emailed link, valid for 1 hour, is opened (`POST /api/auth/confirm-delete`).")
            .Responds<ApiResult>("Confirmation email sent.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Email an account-deletion confirmation link</summary>
    private static async Task<IResult> HandleAsync(
        RequestAccountDeletionRequest request,
        UserService userService,
        AuthActionTokenService tokens,
        ClaimsPrincipal principal,
        ILogger<RequestAccountDeletionRequest> logger)
    {
        if (!principal.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var user = await userService.GetByIdAsync(userId);
        if (user is null) return Results.Unauthorized();

        await tokens.SendDeleteConfirmationAsync(user.Id, user.Email, request.Locale);

        logger.LogInformation("Account-deletion confirmation sent for user {UserId}", user.Id);
        return Results.Ok(new { success = true, message = "Confirmation email sent" });
    }
}
