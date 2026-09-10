using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TaleTrackApp.Auth;
using TaleTrackApp.Features.Auth;
using TaleTrackApp.Model;

namespace TaleTrackApp.Features.User.RequestDeletion;

public class RequestAccountDeletionRequest
{
    /// <summary>UI locale of the requester ("es"/"en").</summary>
    [StringLength(10)]
    public string? Locale { get; set; }
}

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
        EmailService emailService,
        ClaimsPrincipal principal,
        ILogger<RequestAccountDeletionRequest> logger)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Results.Unauthorized();

        var user = await userService.GetByIdAsync(userId);
        if (user is null) return Results.Unauthorized();

        var raw = await tokens.IssueAsync(user.Id, AuthActionToken.DeleteAccount);
        await emailService.SendDeleteConfirmationAsync(user.Email, AppLinks.ConfirmDelete(raw), request.Locale);

        logger.LogInformation("Account-deletion confirmation sent for user {UserId}", user.Id);
        return Results.Ok(new { success = true, message = "Confirmation email sent" });
    }
}
