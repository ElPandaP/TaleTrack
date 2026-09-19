using System.Security.Claims;
using TaleTrackApp.Security;
using TaleTrackApp.Features.User;
using TaleTrackApp.Model;

namespace TaleTrackApp.Features.Auth.RequestDeletion;

public static class RequestAccountDeletionEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/request-account-deletion", HandleAsync)
            .WithName("RequestAccountDeletion")
            .WithDescription("Emails a link that confirms and performs account deletion (requires JWT)")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        RequestAccountDeletionRequest request,
        UserService userService,
        AuthActionTokenService tokens,
        ClaimsPrincipal principal,
        ILogger<RequestAccountDeletionRequest> logger)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        var user = await userService.GetByIdAsync(userId);
        if (user is null) return Results.Unauthorized();

        await tokens.SendDeleteConfirmationAsync(user.Id, user.Email, request.Locale);

        logger.LogInformation("Account-deletion confirmation sent for user {UserId}", user.Id);
        return Results.Ok(new { success = true, message = "Confirmation email sent" });
    }
}
