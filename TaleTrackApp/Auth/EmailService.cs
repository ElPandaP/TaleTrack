using System.Text;
using System.Text.Json;

namespace TaleTrackApp.Auth;

public class EmailService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmailService> _logger;
    private const string FromAddress = "noreply@taletrack.app";

    public EmailService(HttpClient httpClient, ILogger<EmailService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task SendVerificationCodeAsync(string toEmail, string code)
    {
        var payload = new
        {
            from = FromAddress,
            to = new[] { toEmail },
            subject = "Tu código de verificación - TaleTrack",
            html = $"<p>Tu código de verificación es: <strong>{code}</strong></p><p>Expira en 10 minutos.</p>"
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://api.resend.com/emails", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Resend error {Status}: {Error}", response.StatusCode, error);
            throw new Exception("Error al enviar el email de verificación");
        }

        _logger.LogInformation("Verification code sent to {Email}", toEmail);
    }
}
