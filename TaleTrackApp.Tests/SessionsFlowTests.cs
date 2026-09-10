using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Active sessions ("Conexiones"): logging in creates one, granting the browser
/// extension its own token pair creates another, and either can be revoked
/// independently — but only by the user who owns it.
/// </summary>
[Collection(ApiCollection.Name)]
public class SessionsFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private async Task<HttpClient> AuthedClientAsync(string email, string username)
    {
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/register",
            new { Email = email, Username = username, Password = "Password1!" });
        var login = await client.PostAsJsonAsync("/api/login",
            new { Email = email, Password = "Password1!" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return client;
    }

    private static async Task<JsonElement> SessionsAsync(HttpClient client)
    {
        var res = await client.GetAsync("/api/auth/sessions");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Login_CreatesAWebSession()
    {
        var client = await AuthedClientAsync("sessions-login@test.com", "sessionslogin");

        var sessions = await SessionsAsync(client);
        Assert.Contains(sessions.GetProperty("data").EnumerateArray(), s => s.GetProperty("device").GetString() == "Web");
    }

    [Fact]
    public async Task ExtensionGrant_AddsASeparateSession()
    {
        var client = await AuthedClientAsync("sessions-extension@test.com", "sessionsextension");

        var before = await SessionsAsync(client);
        var beforeCount = before.GetProperty("data").GetArrayLength();

        var grantRes = await client.PostAsync("/api/auth/extension-grant", null);
        Assert.True(grantRes.IsSuccessStatusCode, await grantRes.Content.ReadAsStringAsync());

        var after = await SessionsAsync(client);
        Assert.Equal(beforeCount + 1, after.GetProperty("data").GetArrayLength());
        Assert.Contains(after.GetProperty("data").EnumerateArray(),
            s => s.GetProperty("device").GetString() == "Netflix extension");
    }

    [Fact]
    public async Task RevokeSession_RemovesItFromTheList()
    {
        var client = await AuthedClientAsync("sessions-revoke@test.com", "sessionsrevoke");

        var sessions = await SessionsAsync(client);
        var sessionId = sessions.GetProperty("data")[0].GetProperty("id").GetInt32();

        var revokeRes = await client.DeleteAsync($"/api/auth/sessions/{sessionId}");
        Assert.True(revokeRes.IsSuccessStatusCode, await revokeRes.Content.ReadAsStringAsync());

        var after = await SessionsAsync(client);
        Assert.DoesNotContain(after.GetProperty("data").EnumerateArray(), s => s.GetProperty("id").GetInt32() == sessionId);
    }

    [Fact]
    public async Task RevokeSession_BelongingToAnotherUser_Returns404()
    {
        var a = await AuthedClientAsync("sessions-owner@test.com", "sessionsowner");
        var b = await AuthedClientAsync("sessions-stranger@test.com", "sessionsstranger");

        var aSessions = await SessionsAsync(a);
        var aSessionId = aSessions.GetProperty("data")[0].GetProperty("id").GetInt32();

        var res = await b.DeleteAsync($"/api/auth/sessions/{aSessionId}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
