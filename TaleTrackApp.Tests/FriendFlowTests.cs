using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Friendships: finding someone by username, sending, accepting, declining or cancelling a request,
/// removing a friend, the requests that make no sense (to oneself, to nobody, repeated, to a
/// friend, or crossing one already received), and that only the addressee can answer a request.
/// </summary>
[Collection(ApiCollection.Name)]
public class FriendFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static async Task<JsonElement> FriendsAsync(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement>("/api/friends");

    private static bool Lists(JsonElement friends, string list, Guid userId) =>
        friends.GetProperty(list).EnumerateArray().Any(r => r.GetProperty("userId").GetGuid() == userId);

    private static Task<HttpResponseMessage> SendRequestAsync(TestUser from, Guid toId) =>
        from.Client.PostAsJsonAsync("/api/friends/requests", new { UserId = toId });

    [Theory]
    [InlineData(false)]
    [InlineData(true)] // the web lets people type the @ in front
    public async Task SearchUser_ByUsername_FindsThem(bool withAt)
    {
        var caller = await _factory.CreateUserAsync($"friend-search-{withAt}@test.com", $"friendsearch{withAt}");
        var target = $"friendsearchtarget{withAt}".ToLower();
        await _factory.CreateUserAsync($"friend-search-target-{withAt}@test.com", target);

        var res = await caller.Client.GetFromJsonAsync<JsonElement>($"/api/users/search?username={(withAt ? "@" : "")}{target}");
        Assert.Equal(target, res.GetProperty("user").GetProperty("username").GetString());
    }

    [Fact]
    public async Task SearchUser_UnknownUsername_FindsNobody()
    {
        var caller = await _factory.CreateUserAsync("friend-search-none@test.com", "friendsearchnone");

        var res = await caller.Client.GetFromJsonAsync<JsonElement>("/api/users/search?username=nobody-has-this-name");
        Assert.Equal(JsonValueKind.Null, res.GetProperty("user").ValueKind);
    }

    [Fact]
    public async Task SendRequest_ShowsAsOutgoingAndIncoming()
    {
        var a = await _factory.CreateUserAsync("friend-send-a@test.com", "friendsenda");
        var b = await _factory.CreateUserAsync("friend-send-b@test.com", "friendsendb");

        Assert.True((await SendRequestAsync(a, b.Id)).IsSuccessStatusCode);

        Assert.True(Lists(await FriendsAsync(a.Client), "outgoing", b.Id));
        Assert.True(Lists(await FriendsAsync(b.Client), "incoming", a.Id));
    }

    [Fact]
    public async Task AcceptRequest_MakesBothUsersFriends()
    {
        var a = await _factory.CreateUserAsync("friend-accept-a@test.com", "friendaccepta");
        var b = await _factory.CreateUserAsync("friend-accept-b@test.com", "friendacceptb");

        await ApiHelpers.BefriendAsync(a, b);

        Assert.True(Lists(await FriendsAsync(a.Client), "friends", b.Id));
        Assert.True(Lists(await FriendsAsync(b.Client), "friends", a.Id));
    }

    [Theory]
    [InlineData("decline")]
    [InlineData("cancel")]
    [InlineData("remove friend")]
    public async Task EndingARelationship_RemovesItForBoth(string how)
    {
        var a = await _factory.CreateUserAsync($"friend-end-{how.Replace(' ', '-')}-a@test.com", $"friendend{how.Replace(" ", "")}a");
        var b = await _factory.CreateUserAsync($"friend-end-{how.Replace(' ', '-')}-b@test.com", $"friendend{how.Replace(" ", "")}b");

        HttpResponseMessage res;
        switch (how)
        {
            case "decline":
                await SendRequestAsync(a, b.Id);
                res = await b.Client.DeleteAsync($"/api/friends/requests/{await ApiHelpers.IncomingRequestIdAsync(b.Client, a.Id)}");
                break;
            case "cancel":
                await SendRequestAsync(a, b.Id);
                res = await a.Client.DeleteAsync($"/api/friends/{b.Id}");
                break;
            default:
                await ApiHelpers.BefriendAsync(a, b);
                res = await a.Client.DeleteAsync($"/api/friends/{b.Id}");
                break;
        }
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        foreach (var (user, other) in new[] { (a, b), (b, a) })
        {
            var friends = await FriendsAsync(user.Client);
            Assert.False(Lists(friends, "friends", other.Id) || Lists(friends, "incoming", other.Id) || Lists(friends, "outgoing", other.Id));
        }
    }

    [Theory]
    [InlineData("to oneself", "self")]
    [InlineData("to nobody", "target_not_found")]
    [InlineData("repeated", "already_pending")]
    [InlineData("to a friend", "already_friends")]
    [InlineData("crossing one received", "reverse_pending")]
    public async Task SendRequest_ThatMakesNoSense_IsRejectedWithItsCode(string problem, string code)
    {
        var slug = problem.Replace(" ", "");
        var a = await _factory.CreateUserAsync($"friend-bad-{slug}-a@test.com", $"friendbad{slug}a");
        var b = await _factory.CreateUserAsync($"friend-bad-{slug}-b@test.com", $"friendbad{slug}b");
        var target = b.Id;
        switch (problem)
        {
            case "to oneself": target = a.Id; break;
            case "to nobody": target = Guid.NewGuid(); break;
            case "repeated": await SendRequestAsync(a, b.Id); break;
            case "to a friend": await ApiHelpers.BefriendAsync(a, b); break;
            case "crossing one received": await SendRequestAsync(b, a.Id); break;
        }

        var res = await SendRequestAsync(a, target);
        Assert.False(res.IsSuccessStatusCode);
        Assert.Equal(code, await res.CodeAsync());
    }

    [Fact]
    public async Task ReverseRow_ForTheSamePair_IsRejectedByTheDatabase()
    {
        var a = await _factory.CreateUserAsync("friend-pair-a@test.com", "friendpaira");
        var b = await _factory.CreateUserAsync("friend-pair-b@test.com", "friendpairb");
        await SendRequestAsync(a, b.Id);

        // Writes what a request from B to A would add if it raced past the service's own check;
        // the database must still refuse a second row for the same pair.
        using var scope = _factory.NewDbScope(out var db);
        db.Friendships.Add(new Model.Friendship { RequesterId = b.Id, AddresseeId = a.Id });
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("decline")]
    public async Task AnsweringARequestAddressedToSomeoneElse_IsDenied(string answer)
    {
        var a = await _factory.CreateUserAsync($"friend-403-{answer}-a@test.com", $"friend403{answer}a");
        var b = await _factory.CreateUserAsync($"friend-403-{answer}-b@test.com", $"friend403{answer}b");
        var c = await _factory.CreateUserAsync($"friend-403-{answer}-c@test.com", $"friend403{answer}c");
        await SendRequestAsync(a, b.Id);
        var requestId = await ApiHelpers.IncomingRequestIdAsync(b.Client, a.Id);

        var res = answer == "accept"
            ? await c.Client.PutAsync($"/api/friends/requests/{requestId}", null)
            : await c.Client.DeleteAsync($"/api/friends/requests/{requestId}");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.True(Lists(await FriendsAsync(b.Client), "incoming", a.Id));
    }
}
