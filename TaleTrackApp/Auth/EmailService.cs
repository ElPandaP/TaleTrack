using System.Text;
using System.Text.Json;

namespace TaleTrackApp.Auth;

/// <summary>
/// Sends transactional email via Resend. Every message is rendered in the caller's
/// locale ("es"/"en"); anything else falls back to English.
/// </summary>
public class EmailService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmailService> _logger;
    private const string FromAddress = "TaleTrack <noreply@taletrack.app>";

    public EmailService(HttpClient httpClient, ILogger<EmailService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    private static bool IsSpanish(string? locale) =>
        (locale ?? string.Empty).Trim().ToLowerInvariant().StartsWith("es");

    public Task SendVerificationCodeAsync(string toEmail, string code, string? locale)
    {
        var (subject, body) = IsSpanish(locale)
            ? ("Tu código de verificación · TaleTrack",
               $"<p>Tu código de verificación es:</p><p style=\"font-size:24px;font-weight:700;letter-spacing:3px\">{code}</p><p>Caduca en 10 minutos.</p>")
            : ("Your verification code · TaleTrack",
               $"<p>Your verification code is:</p><p style=\"font-size:24px;font-weight:700;letter-spacing:3px\">{code}</p><p>It expires in 10 minutes.</p>");
        return SendAsync(toEmail, subject, Wrap(body));
    }

    public Task SendPasswordResetAsync(string toEmail, string link, string? locale)
    {
        var (subject, body) = IsSpanish(locale)
            ? ("Restablece tu contraseña · TaleTrack",
               "<p>Has pedido restablecer tu contraseña de TaleTrack. Pulsa el botón para elegir una nueva:</p>"
               + Button(link, "Restablecer contraseña")
               + "<p>El enlace caduca en 1 hora. Si no fuiste tú, ignora este correo — tu contraseña no cambia.</p>")
            : ("Reset your password · TaleTrack",
               "<p>You asked to reset your TaleTrack password. Tap the button to choose a new one:</p>"
               + Button(link, "Reset password")
               + "<p>The link expires in 1 hour. If this wasn't you, ignore this email — your password stays the same.</p>");
        return SendAsync(toEmail, subject, Wrap(body));
    }

    public Task SendDeleteConfirmationAsync(string toEmail, string link, string? locale)
    {
        var (subject, body) = IsSpanish(locale)
            ? ("Confirma el borrado de tu cuenta · TaleTrack",
               "<p>Has pedido eliminar tu cuenta de TaleTrack. Esto borra tu biblioteca, tus reseñas y tu seguimiento, y no se puede deshacer.</p>"
               + Button(link, "Eliminar mi cuenta")
               + "<p>El enlace caduca en 1 hora. Si no fuiste tú, ignora este correo.</p>")
            : ("Confirm your account deletion · TaleTrack",
               "<p>You asked to delete your TaleTrack account. This removes your library, reviews and tracking, and can't be undone.</p>"
               + Button(link, "Delete my account")
               + "<p>The link expires in 1 hour. If this wasn't you, ignore this email.</p>");
        return SendAsync(toEmail, subject, Wrap(body));
    }

    public Task SendWelcomeAsync(string toEmail, string revokeLink, string? locale)
    {
        var (subject, body) = IsSpanish(locale)
            ? ("Te damos la bienvenida a TaleTrack",
               "<p>Se ha creado una cuenta de TaleTrack con esta dirección de correo. ¡Bienvenido/a!</p>"
               + "<p>Si no fuiste tú quien se registró, pulsa aquí para eliminar la cuenta:</p>"
               + Button(revokeLink, "No fui yo, eliminar la cuenta")
               + "<p>Este enlace funciona durante 7 días.</p>")
            : ("Welcome to TaleTrack",
               "<p>A TaleTrack account was created with this email address. Welcome!</p>"
               + "<p>If you didn't sign up, tap here to delete the account:</p>"
               + Button(revokeLink, "This wasn't me, delete the account")
               + "<p>This link works for 7 days.</p>");
        return SendAsync(toEmail, subject, Wrap(body));
    }

    private static string Wrap(string inner) =>
        $"<div style=\"font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;max-width:480px;margin:0 auto;color:#2b2b2b;line-height:1.5\">{inner}</div>";

    private static string Button(string href, string label) =>
        $"<p style=\"margin:24px 0\"><a href=\"{href}\" style=\"background:#3f6212;color:#ffffff;padding:11px 20px;border-radius:8px;text-decoration:none;display:inline-block;font-weight:600\">{label}</a></p>";

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

        _logger.LogInformation("Email '{Subject}' sent to {Email}", subject, toEmail);
    }
}
