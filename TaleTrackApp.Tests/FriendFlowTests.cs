using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Send / accept / decline / remove a friend request, plus the "you can't send
/// a duplicate or self request" guards.
/// </summary>
[Collection(ApiCollection.Name)]
public class FriendFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private async Task<(HttpClient Client, Guid UserId)> AuthedClientAsync(string email, string username)
    {
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/register",
            new { Email = email, Username = username, Password = "Password1!" });
        var login = await client.PostAsJsonAsync("/api/login",
            new { Email = email, Password = "Password1!" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());

        var me = await client.GetAsync("/api/users/me");
        var meBody = await me.Content.ReadFromJsonAsync<JsonElement>();
        return (client, meBody.GetProperty("data").GetProperty("id").GetGuid());
    }

    private static async Task<JsonElement> FriendsAsync(HttpClient client)
    {
        var res = await client.GetAsync("/api/friends");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task SendRequest_AppearsInSenderOutgoing_AndTargetIncoming()
    {
        var (a, _) = await AuthedClientAsync("friend-send-a@test.com", "friendsenda");
        var (_, bId) = await AuthedClientAsync("friend-send-b@test.com", "friendsendb");

        var res = await a.PostAsJsonAsync("/api/friends/requests", new { UserId = bId });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        var aFriends = await FriendsAsync(a);
        Assert.Contains(aFriends.GetProperty("outgoing").EnumerateArray(), r => r.GetProperty("userId").GetGuid() == bId);
    }

    [Fact]
    public async Task AcceptRequest_MakesBothUsersFriends()
    {
        var (a, aId) = await AuthedClientAsync("friend-accept-a@test.com", "friendaccepta");
        var (b, bId) = await AuthedClientAsync("friend-accept-b@test.com", "friendacceptb");

        await a.PostAsJsonAsync("/api/friends/requests", new { UserId = bId });
        var bFriendsBefore = await FriendsAsync(b);
        var requestId = bFriendsBefore.GetProperty("incoming").EnumerateArray()
            .First(r => r.GetProperty("userId").GetGuid() == aId)
            .GetProperty("requestId").GetGuid();

        var respondRes = await b.PutAsync($"/api/friends/requests/{requestId}", null);
        Assert.True(respondRes.IsSuccessStatusCode, await respondRes.Content.ReadAsStringAsync());

        var aFriends = await FriendsAsync(a);
        var bFriends = await FriendsAsync(b);
        Assert.Contains(aFriends.GetProperty("friends").EnumerateArray(), f => f.GetProperty("userId").GetGuid() == bId);
        Assert.Contains(bFriends.GetProperty("friends").EnumerateArray(), f => f.GetProperty("userId").GetGuid() == aId);
    }

    [Fact]
    public async Task DeclineRequest_RemovesItForBoth()
    {
        var (a, aId) = await AuthedClientAsync("friend-decline-a@test.com", "frienddeclinea");
        var (b, bId) = await AuthedClientAsync("friend-decline-b@test.com", "frienddeclineb");

        await a.PostAsJsonAsync("/api/friends/requests", new { UserId = bId });
        var bFriendsBefore = await FriendsAsync(b);
        var requestId = bFriendsBefore.GetProperty("incoming").EnumerateArray()
            .First(r => r.GetProperty("userId").GetGuid() == aId)
            .GetProperty("requestId").GetGuid();

        await b.DeleteAsync($"/api/friends/requests/{requestId}");

        var aFriends = await FriendsAsync(a);
        var bFriends = await FriendsAsync(b);
        Assert.DoesNotContain(aFriends.GetProperty("outgoing").EnumerateArray(), r => r.GetProperty("userId").GetGuid() == bId);
        Assert.DoesNotContain(bFriends.GetProperty("incoming").EnumerateArray(), r => r.GetProperty("userId").GetGuid() == aId);
        Assert.DoesNotContain(bFriends.GetProperty("friends").EnumerateArray(), f => f.GetProperty("userId").GetGuid() == aId);
    }

    [Fact]
    public async Task RemoveFriend_RemovesAcceptedFriendship()
    {
        var (a, aId) = await AuthedClientAsync("friend-remove-a@test.com", "friendremovea");
        var (b, bId) = await AuthedClientAsync("friend-remove-b@test.com", "friendremoveb");

        await a.PostAsJsonAsync("/api/friends/requests", new { UserId = bId });
        var bFriendsBefore = await FriendsAsync(b);
        var requestId = bFriendsBefore.GetProperty("incoming").EnumerateArray()
            .First(r => r.GetProperty("userId").GetGuid() == aId)
            .GetProperty("requestId").GetGuid();
        await b.PutAsync($"/api/friends/requests/{requestId}", null);

        var removeRes = await a.DeleteAsync($"/api/friends/{bId}");
        Assert.True(removeRes.IsSuccessStatusCode, await removeRes.Content.ReadAsStringAsync());

        var aFriends = await FriendsAsync(a);
        Assert.DoesNotContain(aFriends.GetProperty("friends").EnumerateArray(), f => f.GetProperty("userId").GetGuid() == bId);
    }

    [Fact]
    public async Task SendRequest_ToSelf_Fails()
    {
        var (a, aId) = await AuthedClientAsync("friend-self@test.com", "friendself");
        var res = await a.PostAsJsonAsync("/api/friends/requests", new { UserId = aId });
        Assert.False(res.IsSuccessStatusCode);
    }

    [Fact]
    public async Task SendRequest_Duplicate_Fails()
    {
        var (a, _) = await AuthedClientAsync("friend-dup-a@test.com", "frienddupa");
        var (_, bId) = await AuthedClientAsync("friend-dup-b@test.com", "frienddupb");

        await a.PostAsJsonAsync("/api/friends/requests", new { UserId = bId });
        var second = await a.PostAsJsonAsync("/api/friends/requests", new { UserId = bId });
        Assert.False(second.IsSuccessStatusCode);
    }
}
