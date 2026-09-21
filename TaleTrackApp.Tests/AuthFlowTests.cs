using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TaleTrackApp.Tests;

[Collection(ApiCollection.Name)]
public class AuthFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private HttpClient NewClient()
    {
        var client = _factory.CreateClient();
        return client;
    }

    private static async Task<JsonElement> RegisterAndLoginAsync(HttpClient client, string email, string username)
    {
        var register = await client.PostAsJsonAsync("/api/register",
            new { Email = email, Username = username, Password = "Password1!" });
        Assert.True(register.IsSuccessStatusCode, $"Register failed: {await register.Content.ReadAsStringAsync()}");

        var login = await client.PostAsJsonAsync("/api/login", new { Email = email, Password = "Password1!" });
        Assert.True(login.IsSuccessStatusCode, $"Login failed: {await login.Content.ReadAsStringAsync()}");
        return await login.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Login_ReturnsAccessAndRefreshToken()
    {
        var client = NewClient();
        var body = await RegisterAndLoginAsync(client, "login-rt@test.com", "loginrt");

        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("refreshToken").GetString()));
        Assert.Equal(3600, body.GetProperty("expiresIn").GetInt32());
    }

    [Fact]
    public async Task Refresh_RotatesTokens_AndReplaysWithinGraceWindow()
    {
        var client = NewClient();
        var body = await RegisterAndLoginAsync(client, "rotate@test.com", "rotateuser");
        var refreshToken = body.GetProperty("refreshToken").GetString()!;

        var first = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        Assert.True(first.IsSuccessStatusCode, $"Refresh failed: {await first.Content.ReadAsStringAsync()}");
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var newRefreshToken = firstBody.GetProperty("refreshToken").GetString()!;
        Assert.NotEqual(refreshToken, newRefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(firstBody.GetProperty("token").GetString()));

        // Reusing the just-consumed token inside the grace window replays the same
        // replacement (so concurrent refreshers don't knock each other out).
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        Assert.True(reuse.IsSuccessStatusCode);
        var reuseBody = await reuse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(newRefreshToken, reuseBody.GetProperty("refreshToken").GetString());

        // The rotated token works and rotates further.
        var second = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = newRefreshToken });
        Assert.True(second.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_Returns401()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "not-a-real-token" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ExtensionGrant_RequiresJwt()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/auth/extension-grant", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ExtensionGrant_ReturnsPair_AndShowsAsSession()
    {
        var client = NewClient();
        var body = await RegisterAndLoginAsync(client, "extgrant@test.com", "extgrant");
        var jwt = body.GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var grantRes = await client.PostAsync("/api/auth/extension-grant", null);
        Assert.True(grantRes.IsSuccessStatusCode, $"Grant failed: {await grantRes.Content.ReadAsStringAsync()}");
        var grant = await grantRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(grant.GetProperty("token").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(grant.GetProperty("refreshToken").GetString()));

        var sessionsRes = await client.GetAsync("/api/auth/sessions");
        var sessions = await sessionsRes.Content.ReadFromJsonAsync<JsonElement>();
        var devices = sessions.GetProperty("data").EnumerateArray()
            .Select(s => s.GetProperty("device").GetString())
            .ToList();
        Assert.Contains("Web", devices);
        Assert.Contains("Netflix extension", devices);
    }

    [Fact]
    public async Task RevokeSession_KillsThatRefreshToken()
    {
        var client = NewClient();
        var body = await RegisterAndLoginAsync(client, "revoke@test.com", "revokeuser");
        var jwt = body.GetProperty("token").GetString()!;
        var refreshToken = body.GetProperty("refreshToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var sessionsRes = await client.GetAsync("/api/auth/sessions");
        var sessions = await sessionsRes.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = sessions.GetProperty("data")[0].GetProperty("id").GetGuid();

        var del = await client.DeleteAsync($"/api/auth/sessions/{sessionId}");
        Assert.True(del.IsSuccessStatusCode);

        var refreshRes = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshRes.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var client = NewClient();
        var body = await RegisterAndLoginAsync(client, "logout@test.com", "logoutuser");
        var refreshToken = body.GetProperty("refreshToken").GetString()!;

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken });
        Assert.True(logout.IsSuccessStatusCode);

        // Token was never rotated, so there's no grace entry — it fails immediately.
        var refreshRes = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshRes.StatusCode);
    }

    [Fact]
    public async Task Logout_UnknownToken_StillOk()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = "nope" });
        Assert.True(res.IsSuccessStatusCode);
    }

    [Fact]
    public async Task LoginByCode_WithWebDevice_IssuesWebSession()
    {
        var client = NewClient();
        await client.PostAsJsonAsync("/api/register",
            new { Email = "code-web@test.com", Username = "codeweb", Password = "Password1!" });
        await client.PostAsJsonAsync("/api/auth/request-code", new { Email = "code-web@test.com" });

        string code;
        using (_factory.NewDbScope(out var db))
        {
            var user = await db.Users.SingleAsync(u => u.Email == "code-web@test.com");
            code = user.EmailCode!;
        }

        var verify = await client.PostAsJsonAsync("/api/auth/verify-code",
            new { Email = "code-web@test.com", Code = code, Device = "Web" });
        Assert.True(verify.IsSuccessStatusCode, $"Verify failed: {await verify.Content.ReadAsStringAsync()}");
        var body = await verify.Content.ReadFromJsonAsync<JsonElement>();
        var jwt = body.GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var sessions = await (await client.GetAsync("/api/auth/sessions")).Content.ReadFromJsonAsync<JsonElement>();
        var devices = sessions.GetProperty("data").EnumerateArray()
            .Select(s => s.GetProperty("device").GetString())
            .ToList();
        Assert.Contains("Web", devices);
    }

    [Fact]
    public async Task LoginByCode_WithoutDevice_DefaultsToKOReaderSession()
    {
        var client = NewClient();
        await client.PostAsJsonAsync("/api/register",
            new { Email = "code-koreader@test.com", Username = "codekoreader", Password = "Password1!" });
        await client.PostAsJsonAsync("/api/auth/request-code", new { Email = "code-koreader@test.com" });

        string code;
        using (_factory.NewDbScope(out var db))
        {
            var user = await db.Users.SingleAsync(u => u.Email == "code-koreader@test.com");
            code = user.EmailCode!;
        }

        var verify = await client.PostAsJsonAsync("/api/auth/verify-code",
            new { Email = "code-koreader@test.com", Code = code });
        Assert.True(verify.IsSuccessStatusCode, $"Verify failed: {await verify.Content.ReadAsStringAsync()}");
        var body = await verify.Content.ReadFromJsonAsync<JsonElement>();
        var jwt = body.GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var sessions = await (await client.GetAsync("/api/auth/sessions")).Content.ReadFromJsonAsync<JsonElement>();
        var devices = sessions.GetProperty("data").EnumerateArray()
            .Select(s => s.GetProperty("device").GetString())
            .ToList();
        Assert.Contains("KOReader", devices);
    }

    [Fact]
    public async Task LoginByCode_WrongCode_Returns401()
    {
        var client = NewClient();
        await client.PostAsJsonAsync("/api/register",
            new { Email = "code-wrong@test.com", Username = "codewrong", Password = "Password1!" });
        await client.PostAsJsonAsync("/api/auth/request-code", new { Email = "code-wrong@test.com" });

        var verify = await client.PostAsJsonAsync("/api/auth/verify-code",
            new { Email = "code-wrong@test.com", Code = "000000" });
        Assert.Equal(HttpStatusCode.Unauthorized, verify.StatusCode);
    }

    [Fact]
    public async Task LoginByCode_FiveWrongGuesses_DiscardTheCode()
    {
        var client = NewClient();
        await client.PostAsJsonAsync("/api/register",
            new { Email = "code-brute@test.com", Username = "codebrute", Password = "Password1!" });
        await client.PostAsJsonAsync("/api/auth/request-code", new { Email = "code-brute@test.com" });

        string code;
        using (_factory.NewDbScope(out var db))
        {
            code = (await db.Users.SingleAsync(u => u.Email == "code-brute@test.com")).EmailCode!;
        }

        for (var i = 0; i < 5; i++)
        {
            var wrong = await client.PostAsJsonAsync("/api/auth/verify-code",
                new { Email = "code-brute@test.com", Code = "000000" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        // Even the right code no longer works: a new one has to be requested.
        var late = await client.PostAsJsonAsync("/api/auth/verify-code",
            new { Email = "code-brute@test.com", Code = code });
        Assert.Equal(HttpStatusCode.Unauthorized, late.StatusCode);

        using (_factory.NewDbScope(out var db))
        {
            Assert.Null((await db.Users.SingleAsync(u => u.Email == "code-brute@test.com")).EmailCode);
        }
    }

    [Fact]
    public async Task LoginByCode_FewWrongGuesses_StillAcceptTheRightCode()
    {
        var client = NewClient();
        await client.PostAsJsonAsync("/api/register",
            new { Email = "code-few@test.com", Username = "codefew", Password = "Password1!" });
        await client.PostAsJsonAsync("/api/auth/request-code", new { Email = "code-few@test.com" });

        string code;
        using (_factory.NewDbScope(out var db))
        {
            code = (await db.Users.SingleAsync(u => u.Email == "code-few@test.com")).EmailCode!;
        }

        for (var i = 0; i < 4; i++)
        {
            await client.PostAsJsonAsync("/api/auth/verify-code",
                new { Email = "code-few@test.com", Code = "000000" });
        }

        var verify = await client.PostAsJsonAsync("/api/auth/verify-code",
            new { Email = "code-few@test.com", Code = code });
        Assert.True(verify.IsSuccessStatusCode, $"Verify failed: {await verify.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task RevokeSession_NotOwned_Returns404()
    {
        var owner = NewClient();
        var ownerBody = await RegisterAndLoginAsync(owner, "owner@test.com", "owneruser");
        owner.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerBody.GetProperty("token").GetString());
        var ownerSessions = await (await owner.GetAsync("/api/auth/sessions")).Content.ReadFromJsonAsync<JsonElement>();
        var ownerSessionId = ownerSessions.GetProperty("data")[0].GetProperty("id").GetGuid();

        var attacker = NewClient();
        var attackerBody = await RegisterAndLoginAsync(attacker, "attacker@test.com", "attacker");
        attacker.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", attackerBody.GetProperty("token").GetString());

        var res = await attacker.DeleteAsync($"/api/auth/sessions/{ownerSessionId}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
