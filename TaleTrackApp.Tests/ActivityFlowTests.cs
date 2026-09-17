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

        var me = await client.GetAsync("/api/user/me");
        var meBody = await me.Content.ReadFromJsonAsync<JsonElement>();
        return (client, meBody.GetProperty("data").GetProperty("id").GetGuid());
    }

    private static async Task BefriendAsync(HttpClient a, Guid aId, HttpClient b, Guid bId)
    {
        await a.PostAsJsonAsync("/api/friends/requests", new { UserId = bId });
        var bFriends = await b.GetAsync("/api/friends");
        var bFriendsBody = await bFriends.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = bFriendsBody.GetProperty("incoming").EnumerateArray()
            .First(r => r.GetProperty("userId").GetGuid() == aId)
            .GetProperty("requestId").GetGuid();
        await b.PostAsJsonAsync($"/api/friends/requests/{requestId}", new { Accept = true });
    }

    private static async Task<JsonElement> ActivityAsync(HttpClient client, string query)
    {
        var res = await client.GetAsync($"/api/activity{query}");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Guid> AddReviewAsync(HttpClient client, Guid mediaId, int rating)
    {
        var res = await client.PostAsJsonAsync("/api/review", new { MediaId = mediaId, Rating = rating });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").GetProperty("id").GetGuid();
    }

    private static async Task SetMoviePrivacyAsync(HttpClient client, Guid userId, bool? progress = null, bool? reviews = null)
    {
        var res = await client.PutAsJsonAsync($"/api/user/{userId}",
            new { Privacy = new { MovieProgress = progress, MovieReviews = reviews } });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TrackingAMovie_ShowsAsStartedAndFinished_InOwnActivity()
    {
        var (client, _) = await AuthedClientAsync("activity-own@test.com", "activityown");
        await client.PostAsJsonAsync("/api/tracking/movies", new { Title = "Drifting Static", Minutes = 90, Progress = 100 });

        var feed = await ActivityAsync(client, "?scope=mine");
        var kinds = feed.GetProperty("data").EnumerateArray()
            .Where(i => i.GetProperty("mediaTitleEN").GetString() == "Drifting Static")
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
            i => i.GetProperty("mediaTitleEN").GetString() == "Borrowed Aurora" && i.GetProperty("userId").GetGuid() == bId);
    }

    [Fact]
    public async Task NonFriendsActivity_NotVisibleInSharedFeed()
    {
        var (a, _) = await AuthedClientAsync("activity-stranger-a@test.com", "activitystrangera");
        var (b, _) = await AuthedClientAsync("activity-stranger-b@test.com", "activitystrangerb");

        await b.PostAsJsonAsync("/api/tracking/movies", new { Title = "Unshared Comet", Minutes = 90, Progress = 50 });

        var feed = await ActivityAsync(a, "?scope=all");
        Assert.DoesNotContain(feed.GetProperty("data").EnumerateArray(),
            i => i.GetProperty("mediaTitleEN").GetString() == "Unshared Comet");
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
            i => i.GetProperty("mediaTitleEN").GetString() == "Open Meridian");
    }

    [Fact]
    public async Task ReviewingAMovie_ShowsAsReviewed_InFriendsSharedFeed()
    {
        var (a, aId) = await AuthedClientAsync("activity-review-a@test.com", "activityreviewa");
        var (b, bId) = await AuthedClientAsync("activity-review-b@test.com", "activityreviewb");
        await BefriendAsync(a, aId, b, bId);

        await b.PostAsJsonAsync("/api/tracking/movies", new { Title = "Reviewed Nebula", Minutes = 90, Progress = 100 });
        var lib = await b.GetAsync("/api/library?type=Movie");
        var mediaId = (await lib.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").EnumerateArray()
            .First(i => i.GetProperty("titleEN").GetString() == "Reviewed Nebula").GetProperty("mediaId").GetGuid();
        await AddReviewAsync(b, mediaId, 8);

        var feed = await ActivityAsync(a, "?scope=friends");
        Assert.Contains(feed.GetProperty("data").EnumerateArray(),
            i => i.GetProperty("kind").GetString() == "reviewed"
                && i.GetProperty("mediaTitleEN").GetString() == "Reviewed Nebula"
                && i.GetProperty("rating").GetInt32() == 8);
    }

    [Fact]
    public async Task DisablingMovieProgressPrivacy_HidesEventsFromFriends_ButNotFromOwner()
    {
        var (a, aId) = await AuthedClientAsync("activity-privacy-progress-a@test.com", "activityprivacyprogressa");
        var (b, bId) = await AuthedClientAsync("activity-privacy-progress-b@test.com", "activityprivacyprogressb");
        await BefriendAsync(a, aId, b, bId);
        await SetMoviePrivacyAsync(b, bId, progress: false);

        await b.PostAsJsonAsync("/api/tracking/movies", new { Title = "Private Comet", Minutes = 90, Progress = 100 });

        var friendsFeed = await ActivityAsync(a, "?scope=friends");
        Assert.DoesNotContain(friendsFeed.GetProperty("data").EnumerateArray(),
            i => i.GetProperty("mediaTitleEN").GetString() == "Private Comet");

        var ownFeed = await ActivityAsync(b, "?scope=mine");
        Assert.Contains(ownFeed.GetProperty("data").EnumerateArray(),
            i => i.GetProperty("mediaTitleEN").GetString() == "Private Comet");
    }

    [Fact]
    public async Task DisablingMovieReviewPrivacy_HidesReviewFromFriends_ButNotFromOwner()
    {
        var (a, aId) = await AuthedClientAsync("activity-privacy-review-a@test.com", "activityprivacyreviewa");
        var (b, bId) = await AuthedClientAsync("activity-privacy-review-b@test.com", "activityprivacyreviewb");
        await BefriendAsync(a, aId, b, bId);
        await SetMoviePrivacyAsync(b, bId, reviews: false);

        await b.PostAsJsonAsync("/api/tracking/movies", new { Title = "Private Aurora", Minutes = 90, Progress = 100 });
        var lib = await b.GetAsync("/api/library?type=Movie");
        var mediaId = (await lib.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").EnumerateArray()
            .First(i => i.GetProperty("titleEN").GetString() == "Private Aurora").GetProperty("mediaId").GetGuid();
        await AddReviewAsync(b, mediaId, 6);

        var friendsFeed = await ActivityAsync(a, "?scope=friends");
        Assert.DoesNotContain(friendsFeed.GetProperty("data").EnumerateArray(),
            i => i.GetProperty("kind").GetString() == "reviewed" && i.GetProperty("mediaTitleEN").GetString() == "Private Aurora");

        var ownFeed = await ActivityAsync(b, "?scope=mine");
        Assert.Contains(ownFeed.GetProperty("data").EnumerateArray(),
            i => i.GetProperty("kind").GetString() == "reviewed" && i.GetProperty("mediaTitleEN").GetString() == "Private Aurora");
    }
}
