using TaleTrackApp.Auth;
using TaleTrackApp.Model;

namespace TaleTrackApp.Features.Auth;

/// <summary>Sent to every newly created account, with a 7-day link that deletes the
/// account for the case where someone signed up with an address they don't own.</summary>
public static class WelcomeEmail
{
    public static void SendInBackground(
        IServiceScopeFactory scopeFactory, int userId, string email, string? locale)
    {
        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var tokens = scope.ServiceProvider.GetRequiredService<AuthActionTokenService>();
            var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
            var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("WelcomeEmail");
            try
            {
                var raw = await tokens.IssueAsync(userId, AuthActionToken.SignupRevoke);
                await emailService.SendWelcomeAsync(email, AppLinks.RevokeSignup(raw), locale);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Welcome email failed for user {UserId}", userId);
            }
        });
    }
}
