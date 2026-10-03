using System.Text;
using System.Text.Json;

namespace TaleTrackApp.Features.Auth;

/// <summary>
/// Sends transactional email via Resend. Every message is rendered in the caller's
/// locale ("es"/"en"); anything else falls back to English. Links in the emails point to the web
/// app at <c>APP_BASE_URL</c>.
/// </summary>
public class EmailService
{
    /// <summary>HTTP client configured with the Resend API key.</summary>
    private readonly HttpClient _httpClient;
    /// <summary>Logger.</summary>
    private readonly ILogger<EmailService> _logger;

    /// <summary>Sender shown in every email.</summary>
    private const string FromAddress = "TaleTrack <noreply@taletrack.app>";

    /// <summary>Creates the service.</summary>
    /// <param name="httpClient">Client already configured with the Resend API key (see <c>Program.cs</c>).</param>
    /// <param name="logger">Logger.</param>
    public EmailService(HttpClient httpClient, ILogger<EmailService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>Whether the locale asks for Spanish ("es", "es-ES"...). Everything else gets English.</summary>
    private static bool IsSpanish(string? locale) =>
        (locale ?? string.Empty).Trim().ToLowerInvariant().StartsWith("es");

    /// <summary>Sends the one-time sign-in code, valid for 10 minutes.</summary>
    /// <param name="toEmail">Recipient.</param>
    /// <param name="code">The six-digit code.</param>
    /// <param name="locale">Language of the email.</param>
    public Task SendVerificationCodeAsync(string toEmail, string code, string? locale)
    {
        var (subject, body) = IsSpanish(locale)
            ? ("Tu código de verificación · TaleTrack",
               $"<p>Tu código de verificación es:</p><p style=\"font-size:24px;font-weight:700;letter-spacing:3px\">{code}</p><p>Caduca en 10 minutos.</p>")
            : ("Your verification code · TaleTrack",
               $"<p>Your verification code is:</p><p style=\"font-size:24px;font-weight:700;letter-spacing:3px\">{code}</p><p>It expires in 10 minutes.</p>");
        return SendAsync(toEmail, subject, Wrap(body));
    }

    /// <summary>Sends the password-reset link, valid for 1 hour.</summary>
    /// <param name="toEmail">Recipient.</param>
    /// <param name="rawToken">Single-use token put in the link.</param>
    /// <param name="locale">Language of the email.</param>
    public Task SendPasswordResetAsync(string toEmail, string rawToken, string? locale)
    {
        var (subject, body) = IsSpanish(locale)
            ? ("Restablece tu contraseña · TaleTrack",
               "<p>Has pedido restablecer tu contraseña de TaleTrack. Pulsa el botón para elegir una nueva:</p>"
               + Button(Link("reset-password", rawToken), "Restablecer contraseña")
               + "<p>El enlace caduca en 1 hora. Si no fuiste tú, ignora este correo — tu contraseña no cambia.</p>")
            : ("Reset your password · TaleTrack",
               "<p>You asked to reset your TaleTrack password. Tap the button to choose a new one:</p>"
               + Button(Link("reset-password", rawToken), "Reset password")
               + "<p>The link expires in 1 hour. If this wasn't you, ignore this email — your password stays the same.</p>");
        return SendAsync(toEmail, subject, Wrap(body));
    }

    /// <summary>Sends the account-deletion confirmation link, valid for 1 hour.</summary>
    /// <param name="toEmail">Recipient.</param>
    /// <param name="rawToken">Single-use token put in the link.</param>
    /// <param name="locale">Language of the email.</param>
    public Task SendDeleteConfirmationAsync(string toEmail, string rawToken, string? locale)
    {
        var (subject, body) = IsSpanish(locale)
            ? ("Confirma el borrado de tu cuenta · TaleTrack",
               "<p>Has pedido eliminar tu cuenta de TaleTrack. Esto borra tu biblioteca, tus reseñas y tu seguimiento, y no se puede deshacer.</p>"
               + Button(Link("confirm-delete", rawToken), "Eliminar mi cuenta")
               + "<p>El enlace caduca en 1 hora. Si no fuiste tú, ignora este correo.</p>")
            : ("Confirm your account deletion · TaleTrack",
               "<p>You asked to delete your TaleTrack account. This removes your library, reviews and tracking, and can't be undone.</p>"
               + Button(Link("confirm-delete", rawToken), "Delete my account")
               + "<p>The link expires in 1 hour. If this wasn't you, ignore this email.</p>");
        return SendAsync(toEmail, subject, Wrap(body));
    }

    /// <summary>Sends the welcome email, with a link valid for 7 days to delete the account if the
    /// recipient did not sign up.</summary>
    /// <param name="toEmail">Recipient.</param>
    /// <param name="rawToken">Single-use token put in the link.</param>
    /// <param name="locale">Language of the email.</param>
    public Task SendWelcomeAsync(string toEmail, string rawToken, string? locale)
    {
        var (subject, body) = IsSpanish(locale)
            ? ("Te damos la bienvenida a TaleTrack",
               "<p>Se ha creado una cuenta de TaleTrack con esta dirección de correo. ¡Bienvenido/a!</p>"
               + "<p>Si no fuiste tú quien se registró, pulsa aquí para eliminar la cuenta:</p>"
               + Button(Link("revoke-signup", rawToken), "No fui yo, eliminar la cuenta")
               + "<p>Este enlace funciona durante 7 días.</p>")
            : ("Welcome to TaleTrack",
               "<p>A TaleTrack account was created with this email address. Welcome!</p>"
               + "<p>If you didn't sign up, tap here to delete the account:</p>"
               + Button(Link("revoke-signup", rawToken), "This wasn't me, delete the account")
               + "<p>This link works for 7 days.</p>");
        return SendAsync(toEmail, subject, Wrap(body));
    }

    /// <summary>Base URL of the web app (<c>APP_BASE_URL</c>), without a trailing slash.</summary>
    private static string AppBaseUrl =>
        (Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost:8090").TrimEnd('/');

    /// <summary>Absolute link into the web app carrying a single-use token.</summary>
    private static string Link(string page, string rawToken) =>
        $"{AppBaseUrl}/{page}?token={Uri.EscapeDataString(rawToken)}";

    /// <summary>Wraps the body in the common layout (font, width, colours).</summary>
    private static string Wrap(string inner) =>
        $"<div style=\"font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;max-width:480px;margin:0 auto;color:#2b2b2b;line-height:1.5\">{inner}</div>";

    /// <summary>Renders a link styled as a button.</summary>
    private static string Button(string href, string label) =>
        $"<p style=\"margin:24px 0\"><a href=\"{href}\" style=\"background:#3f6212;color:#ffffff;padding:11px 20px;border-radius:8px;text-decoration:none;display:inline-block;font-weight:600\">{label}</a></p>";

    /// <summary>Posts the email to the Resend API.</summary>
    /// <exception cref="Exception">Resend rejected the request; the error is logged first.</exception>
    private async Task SendAsync(string toEmail, string subject, string html)
    {
        var payload = new
        {
            from = FromAddress,
            to = new[] { toEmail },
            subject,
            html,
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://api.resend.com/emails", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Resend error {Status}: {Error}", response.StatusCode, error);
            throw new Exception("Failed to send email");
        }

        _logger.LogInformation("Email '{Subject}' sent", subject);
    }
}
