using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// The activity feed: your own started/finished/reviewed events, and visibility
/// rules for other users' activity (friends only, whether viewed via the shared
/// feed or a public profile).
/// </summary>
[Collection(ApiCollection.Name)]
public class ActivityFlowTests(CustomWebApplicationFactory factory)
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

    private static async Task BefriendAsync(HttpClient a, int aId, HttpClient b, int bId)
    {
        await a.PostAsJsonAsync("/api/friends/requests", new { UserId = bId });
        var bFriends = await b.GetAsync("/api/friends");
        var bFriendsBody = await bFriends.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = bFriendsBody.GetProperty("incoming").EnumerateArray()
            .First(r => r.GetProperty("userId").GetInt32() == aId)
            .GetProperty("requestId").GetInt32();
        await b.PostAsJsonAsync($"/api/friends/requests/{requestId}", new { Accept = true });
    }

    private static async Task<JsonElement> ActivityAsync(HttpClient client, string query)
    {
        var res = await client.GetAsync($"/api/activity{query}");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task TrackingAMovie_ShowsAsStartedAndFinished_InOwnActivity()
    {
        var (client, _) = await AuthedClientAsync("activity-own@test.com", "activityown");
        await client.PostAsJsonAsync("/api/tracking/movies", new { Title = "Drifting Static", Minutes = 90, Progress = 100 });

        var feed = await ActivityAsync(client, "?scope=mine");
        var kinds = feed.GetProperty("data").EnumerateArray()
            .Where(i => i.GetProperty("mediaTitle").GetString() == "Drifting Static")
            .Select(i => i.GetProperty("kind").GetString())
            .ToList();

        Assert.Contains("started", kinds);
        Assert.Contains("finished", kinds);
    }

    [Fact]
    public async Task FriendsActivity_VisibleInSharedFeed()
    {
        var (a, aId) = await AuthedClientAsync("activity-friend-a@test.com", "activityfrienda");
        var (b, bId) = await AuthedClientAsync("activity-friend-b@test.com", "activityfriendb");
        await BefriendAsync(a, aId, b, bId);

        await b.PostAsJsonAsync("/api/tracking/movies", new { Title = "Borrowed Aurora", Minutes = 90, Progress = 50 });

        var feed = await ActivityAsync(a, "?scope=friends");
        Assert.Contains(feed.GetProperty("data").EnumerateArray(),
            i => i.GetProperty("mediaTitle").GetString() == "Borrowed Aurora" && i.GetProperty("userId").GetInt32() == bId);
    }

    [Fact]
    public async Task NonFriendsActivity_NotVisibleInSharedFeed()
    {
        var (a, _) = await AuthedClientAsync("activity-stranger-a@test.com", "activitystrangera");
        var (b, _) = await AuthedClientAsync("activity-stranger-b@test.com", "activitystrangerb");

        await b.PostAsJsonAsync("/api/tracking/movies", new { Title = "Unshared Comet", Minutes = 90, Progress = 50 });

        var feed = await ActivityAsync(a, "?scope=all");
        Assert.DoesNotContain(feed.GetProperty("data").EnumerateArray(),
            i => i.GetProperty("mediaTitle").GetString() == "Unshared Comet");
    }

    [Fact]
    public async Task PublicProfileActivity_EmptyForNonFriend()
    {
        var (a, _) = await AuthedClientAsync("activity-profile-a@test.com", "activityprofilea");
        var (b, bId) = await AuthedClientAsync("activity-profile-b@test.com", "activityprofileb");

        await b.PostAsJsonAsync("/api/tracking/movies", new { Title = "Locked Meridian", Minutes = 90, Progress = 50 });

        var viewed = await ActivityAsync(a, $"?userId={bId}");
        Assert.Equal(0, viewed.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task PublicProfileActivity_VisibleForFriend()
    {
        var (a, aId) = await AuthedClientAsync("activity-profile-friend-a@test.com", "activityprofilefrienda");
        var (b, bId) = await AuthedClientAsync("activity-profile-friend-b@test.com", "activityprofilefriendb");
        await BefriendAsync(a, aId, b, bId);

        await b.PostAsJsonAsync("/api/tracking/movies", new { Title = "Open Meridian", Minutes = 90, Progress = 50 });

        var viewed = await ActivityAsync(a, $"?userId={bId}");
        Assert.Contains(viewed.GetProperty("data").EnumerateArray(),
            i => i.GetProperty("mediaTitle").GetString() == "Open Meridian");
    }
}
