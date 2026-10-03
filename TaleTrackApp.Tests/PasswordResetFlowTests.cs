using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Resetting a forgotten password: the request never reveals whether an email is registered or
/// how it signs in, and the link in the email sets a new password that follows the sign-up rules,
/// ends every session and works only once and only for an hour.
/// </summary>
[Collection(ApiCollection.Name)]
public class PasswordResetFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private Task<HttpResponseMessage> RequestResetAsync(string email) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/request-password-reset", new { Email = email, Locale = "en" });

    /// <summary>Asks for a reset of <paramref name="email"/> and returns the token of the emailed link.</summary>
    private async Task<string> ResetLinkTokenAsync(string email)
    {
        Assert.Equal(HttpStatusCode.OK, (await RequestResetAsync(email)).StatusCode);
        return (await _factory.Resend.WaitForAsync(email, "Reset your password")).LinkToken!;
    }

    private Task<HttpResponseMessage> ResetAsync(string token, string password) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password", new { Token = token, Password = password });

    private async Task<bool> CanLogInAsync(string email, string password) =>
        (await _factory.CreateClient().PostAsJsonAsync("/api/login", new { Email = email, Password = password })).IsSuccessStatusCode;

    [Fact]
    public async Task RequestReset_ForAnAccountWithPassword_EmailsTheLink()
    {
        await _factory.CreateUserAsync("reset-known@test.com", "resetknown");

        var token = await ResetLinkTokenAsync("reset-known@test.com");
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Theory]
    [InlineData("unknown email")]
    [InlineData("account without password")]
    public async Task RequestReset_WhenThereIsNothingToReset_GivesTheSameAnswer_AndSendsNothing(string problem)
    {
        var email = $"reset-nothing-{problem.Replace(' ', '-')}@test.com";
        if (problem == "account without password")
        {
            var google = await _factory.CreateClient().PostAsJsonAsync("/api/auth/google",
                new { IdToken = FakeGoogleIdTokenValidator.Token("google-reset-nothing", email) });
            var pending = (await google.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("pendingToken").GetString();
            await _factory.CreateClient().PostAsJsonAsync("/api/auth/google/complete",
                new { PendingToken = pending, Username = "resetnothinggoogle" });
        }

        Assert.Equal(HttpStatusCode.OK, (await RequestResetAsync(email)).StatusCode);
        // The email is sent in the background after the response, so it gets a moment to arrive.
        await Task.Delay(500);
        Assert.DoesNotContain(_factory.Resend.SentTo(email), e => e.Subject.Contains("Reset"));
    }

    [Fact]
    public async Task ResetPassword_ChangesThePassword_AndEndsEverySession()
    {
        var user = await _factory.CreateUserAsync("reset-flow@test.com", "resetflow");
        var token = await ResetLinkTokenAsync("reset-flow@test.com");

        var reset = await ResetAsync(token, "BrandNew1");
        Assert.True(reset.IsSuccessStatusCode, await reset.Content.ReadAsStringAsync());

        Assert.False(await CanLogInAsync("reset-flow@test.com", CustomWebApplicationFactory.TestPassword));
        Assert.True(await CanLogInAsync("reset-flow@test.com", "BrandNew1"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().RefreshAsync(user.RefreshToken)).StatusCode);
    }

    [Theory]
    [InlineData("Short1A")]      // 7 characters
    [InlineData("alllower1")]    // no uppercase letter
    [InlineData("NoDigitsHere")] // no number
    public async Task ResetPassword_ToAPasswordBreakingThePolicy_IsRejected(string password)
    {
        var email = $"reset-weak-{password.ToLower()}@test.com";
        await _factory.CreateUserAsync(email, $"resetweak{password.ToLower()}");
        var token = await ResetLinkTokenAsync(email);

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(token, password)).StatusCode);
        Assert.True(await CanLogInAsync(email, CustomWebApplicationFactory.TestPassword));
    }

    [Theory]
    [InlineData("used")]
    [InlineData("expired")]
    [InlineData("tampered")]
    public async Task ResetPassword_WithAnInvalidLink_ChangesNothing(string problem)
    {
        var email = $"reset-badlink-{problem}@test.com";
        await _factory.CreateUserAsync(email, $"resetbadlink{problem}");
        var token = await ResetLinkTokenAsync(email);

        switch (problem)
        {
            case "used":
                Assert.True((await ResetAsync(token, "FirstNew1")).IsSuccessStatusCode);
                break;
            case "expired":
                await _factory.UpdateActionTokenAsync(token, t => t.ExpiresAt = DateTime.UtcNow.AddMinutes(-1));
                break;
            case "tampered":
                token += "x";
                break;
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(token, "SecondNew1")).StatusCode);
        Assert.False(await CanLogInAsync(email, "SecondNew1"));
    }
}
