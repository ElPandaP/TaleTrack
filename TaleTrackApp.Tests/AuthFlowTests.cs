using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Logging in and keeping the session: login with a password, refresh-token rotation (including
/// two refreshes racing with the same token), logout, and logging in with an emailed one-time
/// code, with its wrong-guess limit and its expiry.
/// </summary>
[Collection(ApiCollection.Name)]
public class AuthFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private Task<HttpResponseMessage> LoginAsync(string email, string password = CustomWebApplicationFactory.TestPassword) =>
        _factory.CreateClient().PostAsJsonAsync("/api/login", new { Email = email, Password = password });

    /// <summary>Asks for a sign-in code for <paramref name="email"/> and returns the code from the email.</summary>
    private async Task<string> RequestCodeAsync(string email)
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/request-code", new { Email = email });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await _factory.Resend.WaitForAsync(email, "verification code")).Code!;
    }

    private Task<HttpResponseMessage> VerifyCodeAsync(string email, string code, string? device = null) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/verify-code", new { Email = email, Code = code, Device = device });

    [Fact]
    public async Task Login_ReturnsBothTokens_AndOpensAWebSession()
    {
        await _factory.CreateUserAsync("login-ok@test.com", "loginok");

        var res = await LoginAsync("login-ok@test.com");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("refreshToken").GetString()));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        Assert.Contains((await client.SessionsAsync()).EnumerateArray(), s => s.GetProperty("device").GetString() == "Web");
    }

    [Fact]
    public async Task Login_WithTheEmailInAnotherCase_Works()
    {
        await _factory.CreateUserAsync("login-case@test.com", "logincase");

        var res = await LoginAsync("LOGIN-Case@Test.com");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("wrong password")]
    [InlineData("unknown email")]
    [InlineData("account without password")]
    public async Task Login_ThatFails_AlwaysGetsTheSameAnswer(string problem)
    {
        var email = $"login-fail-{problem.Replace(' ', '-')}@test.com";
        switch (problem)
        {
            case "wrong password":
                await _factory.CreateUserAsync(email, "loginfailwrong");
                break;
            case "account without password":
                // An account created with Google has no password at all.
                var google = await _factory.CreateClient().PostAsJsonAsync("/api/auth/google",
                    new { IdToken = FakeGoogleIdTokenValidator.Token("google-login-fail", email) });
                var pending = (await google.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("pendingToken").GetString();
                await _factory.CreateClient().PostAsJsonAsync("/api/auth/google/complete",
                    new { PendingToken = pending, Username = "loginfailgoogle" });
                break;
        }

        var res = await LoginAsync(email, "NotThePassword1");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal("invalid_credentials", await res.CodeAsync());
    }

    [Fact]
    public async Task Refresh_RotatesTokens_AndReplaysWithinGraceWindow()
    {
        var user = await _factory.CreateUserAsync("rotate@test.com", "rotateuser");
        var client = _factory.CreateClient();

        var first = await client.RefreshAsync(user.RefreshToken);
        Assert.True(first.IsSuccessStatusCode, await first.Content.ReadAsStringAsync());
        var newRefreshToken = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refreshToken").GetString()!;
        Assert.NotEqual(user.RefreshToken, newRefreshToken);

        // Reusing the just-consumed token inside the grace window replays the same
        // replacement (so concurrent refreshers don't knock each other out).
        var reuse = await client.RefreshAsync(user.RefreshToken);
        Assert.True(reuse.IsSuccessStatusCode);
        Assert.Equal(newRefreshToken, (await reuse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refreshToken").GetString());

        // The rotated token works and rotates further.
        Assert.True((await client.RefreshAsync(newRefreshToken)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Refresh_ConcurrentWithTheSameToken_BothGetTheSameWorkingReplacement()
    {
        var user = await _factory.CreateUserAsync("rotate-race@test.com", "rotaterace");
        var client = _factory.CreateClient();

        // Two tabs refreshing at once: neither may end up holding a token the other overwrote.
        var responses = await Task.WhenAll(client.RefreshAsync(user.RefreshToken), client.RefreshAsync(user.RefreshToken));
        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode));

        var tokens = await Task.WhenAll(responses.Select(async r =>
            (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refreshToken").GetString()!));
        Assert.Equal(tokens[0], tokens[1]);
        Assert.True((await client.RefreshAsync(tokens[0])).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Refresh_WithAMadeUpToken_IsRejected()
    {
        var res = await _factory.CreateClient().RefreshAsync("not-a-real-token");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsThatSessionOnly()
    {
        var user = await _factory.CreateUserAsync("logout@test.com", "logoutuser");
        var otherDevice = (await (await LoginAsync("logout@test.com")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("refreshToken").GetString()!;
        var client = _factory.CreateClient();

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = user.RefreshToken });
        Assert.True(logout.IsSuccessStatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.RefreshAsync(user.RefreshToken)).StatusCode);
        Assert.True((await client.RefreshAsync(otherDevice)).IsSuccessStatusCode);
    }

    [Theory]
    [InlineData("Web", "Web")]
    [InlineData(null, "KOReader")] // the KOReader plugin sends no device
    public async Task LoginByCode_OpensASessionForTheDevice(string? device, string expectedDevice)
    {
        var email = $"code-device-{expectedDevice.ToLower()}@test.com";
        await _factory.CreateUserAsync(email, $"codedevice{expectedDevice.ToLower()}");
        var code = await RequestCodeAsync(email);

        var verify = await VerifyCodeAsync(email, code, device);
        Assert.True(verify.IsSuccessStatusCode, await verify.Content.ReadAsStringAsync());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await verify.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        Assert.Contains((await client.SessionsAsync()).EnumerateArray(), s => s.GetProperty("device").GetString() == expectedDevice);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(5, false)] // the fifth wrong guess discards the code
    public async Task LoginByCode_AfterWrongGuesses_TheRightCodeWorksOnlyBelowTheLimit(int wrongGuesses, bool accepted)
    {
        var email = $"code-guesses-{wrongGuesses}@test.com";
        await _factory.CreateUserAsync(email, $"codeguesses{wrongGuesses}");
        var code = await RequestCodeAsync(email);
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 0; i < wrongGuesses; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyCodeAsync(email, wrong)).StatusCode);

        Assert.Equal(accepted, (await VerifyCodeAsync(email, code)).IsSuccessStatusCode);
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(11, false)] // the code lasts 10 minutes
    public async Task LoginByCode_ExpiresAfterTenMinutes(int minutesLater, bool accepted)
    {
        var email = $"code-expiry-{minutesLater}@test.com";
        await _factory.CreateUserAsync(email, $"codeexpiry{minutesLater}");
        var code = await RequestCodeAsync(email);

        // Moves the stored expiry back instead of waiting.
        using (_factory.NewDbScope(out var db))
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.EmailCodeExpiry = user.EmailCodeExpiry!.Value.AddMinutes(-minutesLater);
            await db.SaveChangesAsync();
        }

        Assert.Equal(accepted, (await VerifyCodeAsync(email, code)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task RequestCode_WhileTheEmailServiceIsDown_ReportsTheError()
    {
        var email = $"{FakeResend.DownMarker}-code@test.com";
        await _factory.CreateUserAsync(email, "coderesenddown");

        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/request-code", new { Email = email });
        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);
    }
}
