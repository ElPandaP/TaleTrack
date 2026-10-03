using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Editing the account: the username follows the sign-up rules and cannot take someone else's
/// (in any letter case), changing only the case of one's own is fine, and nobody can edit an
/// account that is not theirs.
/// </summary>
[Collection(ApiCollection.Name)]
public class UserProfileFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static async Task<string?> MyUsernameAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/users/me")).GetProperty("data").GetProperty("username").GetString();

    [Theory]
    [InlineData("ProfileEditCase", true)] // only the case of the user's own name changes
    [InlineData("profilenewname", true)]
    [InlineData("ab", false)]             // 2 characters
    [InlineData(null, false)]             // 51 characters
    public async Task EditUsername_FollowsTheSignupRules(string? newUsername, bool accepted)
    {
        newUsername ??= new string('p', 51);
        var user = await _factory.CreateUserAsync($"profile-edit-{newUsername.Length}-{accepted}@test.com",
            newUsername == "ProfileEditCase" ? "profileeditcase" : $"profileedit{newUsername.Length}");

        var res = await user.Client.PutAsJsonAsync($"/api/users/{user.Id}", new { Username = newUsername });

        Assert.Equal(accepted, res.IsSuccessStatusCode);
        if (accepted) Assert.Equal(newUsername, await MyUsernameAsync(user.Client));
    }

    [Fact]
    public async Task EditUsername_ToSomeoneElsesInAnotherCase_IsRejected()
    {
        await _factory.CreateUserAsync("profile-taken-owner@test.com", "profiletakenname");
        var user = await _factory.CreateUserAsync("profile-taken@test.com", "profiletakentry");

        var res = await user.Client.PutAsJsonAsync($"/api/users/{user.Id}", new { Username = "ProfileTakenName" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("username_taken", await res.CodeAsync());
        Assert.Equal("profiletakentry", await MyUsernameAsync(user.Client));
    }

    [Fact]
    public async Task EditUser_AnotherUsersAccount_IsDenied()
    {
        var a = await _factory.CreateUserAsync("profile-hijack-a@test.com", "profilehijacka");
        var b = await _factory.CreateUserAsync("profile-hijack-b@test.com", "profilehijackb");

        var res = await a.Client.PutAsJsonAsync($"/api/users/{b.Id}", new { Username = "hijacked" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("profilehijackb", await MyUsernameAsync(b.Client));
    }
}
