using System.ComponentModel.DataAnnotations;
using TaleTrackApp.Auth;
using TaleTrackApp.Features.User;
using TaleTrackApp.Model;

namespace TaleTrackApp.Features.Auth.PasswordReset;

public class RequestPasswordResetRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email must be valid")]
    public required string Email { get; set; }

    /// <summary>UI locale of the requester ("es"/"en"); anything else is treated as English.</summary>
    [StringLength(10)]
    public string? Locale { get; set; }
}

public static class RequestPasswordResetEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/request-password-reset", HandleAsync)
            .WithName("RequestPasswordReset")
            .WithDescription("Emails a password-reset link if the account exists and has a password")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    // Fire-and-forget so the response is instant and identical whether or not the
    // email exists (no account enumeration).
    private static IResult HandleAsync(
        RequestPasswordResetRequest request,
        IServiceScopeFactory scopeFactory)
    {
        var (email, locale) = (request.Email, request.Locale);

        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserService>();
            var tokens = scope.ServiceProvider.GetRequiredService<AuthActionTokenService>();
            var emailSvc = scope.ServiceProvider.GetRequiredService<EmailService>();
            var log = scope.ServiceProvider.GetRequiredService<ILogger<RequestPasswordResetRequest>>();
            try
            {
                var user = await users.GetByEmailAsync(email);
                if (user is null || string.IsNullOrEmpty(user.PasswordHash)) return;

                var raw = await tokens.IssueAsync(user.Id, AuthActionToken.PasswordReset);
                await emailSvc.SendPasswordResetAsync(user.Email, AppLinks.PasswordReset(raw), locale);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Password-reset email failed");
            }
        });

        return Results.Ok(new { success = true });
    }
}
