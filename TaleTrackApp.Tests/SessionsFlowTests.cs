using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Active sessions (the "Conexiones" page): one per client (web, browser extension, KOReader),
/// renewing a session keeps it as the same entry, and each one can be revoked on its own, but
/// only by the user who owns it.
/// </summary>
[Collection(ApiCollection.Name)]
public class SessionsFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    /// <summary>Opens a browser-extension session for the user and returns its refresh token.</summary>
    private static async Task<string> GrantExtensionAsync(TestUser user)
    {
        var res = await user.Client.PostAsync("/api/auth/extension-grant", null);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refreshToken").GetString()!;
    }

    private static Guid SessionId(JsonElement sessions, string device) =>
        sessions.EnumerateArray().First(s => s.GetProperty("device").GetString() == device).GetProperty("id").GetGuid();

    [Fact]
    public async Task Sessions_ListOneEntryPerClient()
    {
        var user = await _factory.CreateUserAsync("sessions-list@test.com", "sessionslist");
        await GrantExtensionAsync(user);
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/request-code", new { Email = "sessions-list@test.com" });
        var code = (await _factory.Resend.WaitForAsync("sessions-list@test.com", "verification code")).Code;
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/verify-code", new { Email = "sessions-list@test.com", Code = code });

        var devices = (await user.Client.SessionsAsync()).EnumerateArray().Select(s => s.GetProperty("device").GetString()).ToList();
        Assert.Equal(["KOReader", "Netflix extension", "Web"], devices.Order().ToList());
    }

    [Fact]
    public async Task Refresh_KeepsTheSameSessionEntry()
    {
        var user = await _factory.CreateUserAsync("sessions-rotated@test.com", "sessionsrotated");
        var listedId = SessionId(await user.Client.SessionsAsync(), "Web");

        Assert.True((await user.Client.RefreshAsync(user.RefreshToken)).IsSuccessStatusCode);

        var after = await user.Client.SessionsAsync();
        Assert.Equal(1, after.GetArrayLength());
        Assert.Equal(listedId, after[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task RevokeSession_EndsThatDeviceOnly()
    {
        var user = await _factory.CreateUserAsync("sessions-revoke@test.com", "sessionsrevoke");
        var extensionRefresh = await GrantExtensionAsync(user);
        var extensionId = SessionId(await user.Client.SessionsAsync(), "Netflix extension");

        var revoke = await user.Client.DeleteAsync($"/api/auth/sessions/{extensionId}");
        Assert.True(revoke.IsSuccessStatusCode, await revoke.Content.ReadAsStringAsync());

        Assert.DoesNotContain((await user.Client.SessionsAsync()).EnumerateArray(), s => s.GetProperty("id").GetGuid() == extensionId);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.RefreshAsync(extensionRefresh)).StatusCode);
        Assert.True((await user.Client.RefreshAsync(user.RefreshToken)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task RevokeSession_OfAnotherUser_IsDenied_AndTheSessionStaysActive()
    {
        var owner = await _factory.CreateUserAsync("sessions-owner@test.com", "sessionsowner");
        var stranger = await _factory.CreateUserAsync("sessions-stranger@test.com", "sessionsstranger");
        var ownerSessionId = SessionId(await owner.Client.SessionsAsync(), "Web");

        var res = await stranger.Client.DeleteAsync($"/api/auth/sessions/{ownerSessionId}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.True((await owner.Client.RefreshAsync(owner.RefreshToken)).IsSuccessStatusCode);
    }
}
