using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaleTrackApp.Data;
using TaleTrackApp.Features.Auth;
using TaleTrackApp.Model;
using Xunit;

namespace TaleTrackApp.Tests;

[Collection(ApiCollection.Name)]
public class PasswordResetFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private HttpClient NewClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", CustomWebApplicationFactory.TestInternalApiKey);
        return client;
    }

    private async Task<int> RegisterAsync(HttpClient client, string email, string username)
    {
        var register = await client.PostAsJsonAsync("/api/register",
            new { Email = email, Username = username, Password = "Password1!" });
        Assert.True(register.IsSuccessStatusCode, await register.Content.ReadAsStringAsync());

        using var scope = _factory.NewDbScope(out var db);
        return (await db.Users.FirstAsync(u => u.Email == email)).Id;
    }

    private async Task<string> IssueTokenAsync(int userId, string purpose)
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<AuthActionTokenService>();
        return await tokens.IssueAsync(userId, purpose);
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        await client.PostAsJsonAsync("/api/login", new { Email = email, Password = password });

    [Fact]
    public async Task RequestPasswordReset_AlwaysReturns200_ForKnownAndUnknownEmail()
    {
        var client = NewClient();
        await RegisterAsync(client, "reset-known@test.com", "resetknown");

        var known = await client.PostAsJsonAsync("/api/auth/request-password-reset",
            new { Email = "reset-known@test.com", Locale = "es" });
        var unknown = await client.PostAsJsonAsync("/api/auth/request-password-reset",
            new { Email = "nobody-here@test.com", Locale = "en" });

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ChangesPassword_KillsSessions_AndIsSingleUse()
    {
        var client = NewClient();
        var userId = await RegisterAsync(client, "reset-flow@test.com", "resetflow");

        var loginBefore = await LoginAsync(client, "reset-flow@test.com", "Password1!");
        Assert.True(loginBefore.IsSuccessStatusCode);
        var oldRefresh = (await loginBefore.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("refreshToken").GetString()!;

        var token = await IssueTokenAsync(userId, AuthActionToken.PasswordReset);

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { Token = token, Password = "BrandNew1!" });
        Assert.True(reset.IsSuccessStatusCode, await reset.Content.ReadAsStringAsync());

        // Old password no longer works, new one does.
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, "reset-flow@test.com", "Password1!")).StatusCode);
        Assert.True((await LoginAsync(client, "reset-flow@test.com", "BrandNew1!")).IsSuccessStatusCode);

        // Sessions issued before the reset are dead.
        var refreshRes = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = oldRefresh });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshRes.StatusCode);

        // The token can't be replayed.
        var replay = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { Token = token, Password = "Another1!" });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithBogusToken_Returns400()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { Token = "not-a-real-token", Password = "Whatever1!" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithExpiredToken_Returns400()
    {
        var client = NewClient();
        var userId = await RegisterAsync(client, "reset-expired@test.com", "resetexpired");
        var token = await IssueTokenAsync(userId, AuthActionToken.PasswordReset);

        using (var scope = _factory.NewDbScope(out var db))
        {
            var hash = AuthActionTokenService.Hash(token);
            var row = await db.AuthActionTokens.FirstAsync(t => t.TokenHash == hash);
            row.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var res = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { Token = token, Password = "Whatever1!" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
