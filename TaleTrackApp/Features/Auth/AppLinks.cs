namespace TaleTrackApp.Features.Auth;

/// <summary>Builds absolute links into the web app for email messages.</summary>
public static class AppLinks
{
    private static string Base =>
        (Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost:8090").TrimEnd('/');

    public static string PasswordReset(string rawToken) =>
        $"{Base}/reset-password?token={Uri.EscapeDataString(rawToken)}";

    public static string ConfirmDelete(string rawToken) =>
        $"{Base}/confirm-delete?token={Uri.EscapeDataString(rawToken)}";

    public static string RevokeSignup(string rawToken) =>
        $"{Base}/revoke-signup?token={Uri.EscapeDataString(rawToken)}";
}
