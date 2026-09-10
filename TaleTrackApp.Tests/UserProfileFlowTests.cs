using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Editing/deleting your own account (never someone else's), and looking other
/// users up — by username (for adding friends) or by id (their public profile).
/// </summary>
[Collection(ApiCollection.Name)]
public class UserProfileFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private async Task<(HttpClient Client, int UserId)> AuthedClientAsync(string email, string username)
    {
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/register",
            new { Email = email, Username = username, Password = "Password1!" });
        var login = await client.PostAsJsonAsync("/api/login",
            new { Email = email, Password = "Password1!" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());

        var me = await client.GetAsync("/api/user/me");
        var meBody = await me.Content.ReadFromJsonAsync<JsonElement>();
        return (client, meBody.GetProperty("data").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task EditUser_UpdatesOwnUsername()
    {
        var (client, id) = await AuthedClientAsync("profile-edit@test.com", "profileeditold");

        var res = await client.PutAsJsonAsync($"/api/user/{id}", new { Username = "profileeditnew" });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        var me = await client.GetAsync("/api/user/me");
        var meBody = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("profileeditnew", meBody.GetProperty("data").GetProperty("username").GetString());
    }

    [Fact]
    public async Task EditUser_AnotherUsersAccount_Returns403()
    {
        var (a, _) = await AuthedClientAsync("profile-edit-a@test.com", "profileedita");
        var (_, bId) = await AuthedClientAsync("profile-edit-b@test.com", "profileeditb");

        var res = await a.PutAsJsonAsync($"/api/user/{bId}", new { Username = "hijacked" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // Account deletion now goes through an emailed confirmation link — see AccountDeletionFlowTests.
    [Fact]
    public async Task RequestAccountDeletion_ForSelf_Succeeds()
    {
        var (client, _) = await AuthedClientAsync("profile-delete@test.com", "profiledelete");

        var res = await client.PostAsJsonAsync("/api/auth/request-account-deletion", new { Locale = "en" });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SearchUsers_FindsExistingUser_ByUsername_WithOrWithoutAtSign()
    {
        var (client, _) = await AuthedClientAsync("search-caller@test.com", "searchcaller");
        await AuthedClientAsync("search-target@test.com", "searchtarget");

        var plain = await client.GetAsync("/api/users/search?username=searchtarget");
        var plainBody = await plain.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("searchtarget", plainBody.GetProperty("user").GetProperty("username").GetString());

        var withAt = await client.GetAsync("/api/users/search?username=@searchtarget");
        var withAtBody = await withAt.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("searchtarget", withAtBody.GetProperty("user").GetProperty("username").GetString());
    }

    [Fact]
    public async Task SearchUsers_UnknownUsername_ReturnsNullUser()
    {
        var (client, _) = await AuthedClientAsync("search-empty@test.com", "searchempty");

        var res = await client.GetAsync("/api/users/search?username=nobody-has-this-name-xyz");
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("user").ValueKind);
    }

    [Fact]
    public async Task GetUserProfile_ShowsPublicCountsAndRelationship()
    {
        var (viewer, _) = await AuthedClientAsync("public-profile-viewer@test.com", "publicprofileviewer");
        var (target, targetId) = await AuthedClientAsync("public-profile-target@test.com", "publicprofiletarget");
        await target.PostAsJsonAsync("/api/tracking/movies", new { Title = "Visible Nebula", Minutes = 90, Progress = 100 });

        var res = await viewer.GetAsync($"/api/users/{targetId}");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("none", body.GetProperty("data").GetProperty("relationship").GetString());
        Assert.Equal(1, body.GetProperty("data").GetProperty("counts").GetProperty("movie").GetInt32());
    }
}
