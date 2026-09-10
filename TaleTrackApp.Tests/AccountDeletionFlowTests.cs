using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaleTrackApp.Features.Auth;
using TaleTrackApp.Model;
using Xunit;

namespace TaleTrackApp.Tests;

[Collection(ApiCollection.Name)]
public class AccountDeletionFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private HttpClient NewClient()
    {
        var client = _factory.CreateClient();
        return client;
    }

    private async Task<(int UserId, string Jwt)> RegisterAndLoginAsync(HttpClient client, string email, string username)
    {
        var register = await client.PostAsJsonAsync("/api/register",
            new { Email = email, Username = username, Password = "Password1!" });
        Assert.True(register.IsSuccessStatusCode, await register.Content.ReadAsStringAsync());

        var login = await client.PostAsJsonAsync("/api/login", new { Email = email, Password = "Password1!" });
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
        var jwt = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;

        using var scope = _factory.NewDbScope(out var db);
        var id = (await db.Users.FirstAsync(u => u.Email == email)).Id;
        return (id, jwt);
    }

    private async Task<string> IssueTokenAsync(int userId, string purpose)
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<AuthActionTokenService>();
        return await tokens.IssueAsync(userId, purpose);
    }

    private async Task<bool> UserExistsAsync(int userId)
    {
        using var scope = _factory.NewDbScope(out var db);
        return await db.Users.AnyAsync(u => u.Id == userId);
    }

    [Fact]
    public async Task RequestAccountDeletion_RequiresAuth()
    {
        var res = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/request-account-deletion", new { Locale = "en" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ConfirmDelete_RemovesTheAccount()
    {
        var client = NewClient();
        var (userId, jwt) = await RegisterAndLoginAsync(client, "del-confirm@test.com", "delconfirm");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var request = await client.PostAsJsonAsync("/api/auth/request-account-deletion", new { Locale = "es" });
        Assert.True(request.IsSuccessStatusCode, await request.Content.ReadAsStringAsync());

        var token = await IssueTokenAsync(userId, AuthActionToken.DeleteAccount);
        var confirm = await client.PostAsJsonAsync("/api/auth/confirm-delete", new { Token = token });
        Assert.True(confirm.IsSuccessStatusCode, await confirm.Content.ReadAsStringAsync());

        Assert.False(await UserExistsAsync(userId));
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/login",
            new { Email = "del-confirm@test.com", Password = "Password1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task ConfirmDelete_WithBogusToken_Returns400()
    {
        var res = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/confirm-delete", new { Token = "nope" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task RevokeSignup_RemovesTheAccount()
    {
        var client = NewClient();
        var (userId, _) = await RegisterAndLoginAsync(client, "del-revoke@test.com", "delrevoke");

        var token = await IssueTokenAsync(userId, AuthActionToken.SignupRevoke);
        var revoke = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/revoke-signup", new { Token = token });
        Assert.True(revoke.IsSuccessStatusCode, await revoke.Content.ReadAsStringAsync());

        Assert.False(await UserExistsAsync(userId));
    }

    [Fact]
    public async Task Token_OfWrongPurpose_IsRejected()
    {
        var client = NewClient();
        var (userId, _) = await RegisterAndLoginAsync(client, "del-wrongpurpose@test.com", "delwrong");

        // A delete_account token must not work on the revoke-signup endpoint.
        var deleteToken = await IssueTokenAsync(userId, AuthActionToken.DeleteAccount);
        var res = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/revoke-signup", new { Token = deleteToken });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.True(await UserExistsAsync(userId));
    }
}
