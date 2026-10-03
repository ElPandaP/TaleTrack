using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// The activity feed and public profiles: a user's own started, finished and reviewed events,
/// friends seeing them and strangers not, and the privacy switches, which hide one kind of event
/// for one type of media from friends while the owner still sees everything.
/// </summary>
[Collection(ApiCollection.Name)]
public class ActivityFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static async Task<List<(string? Kind, string? Title)>> ActivityAsync(HttpClient client, string query)
    {
        var feed = await client.GetFromJsonAsync<JsonElement>($"/api/activity{query}");
        return feed.GetProperty("data").EnumerateArray()
            .Select(i => (i.GetProperty("kind").GetString(), i.GetProperty("mediaTitleEN").GetString()))
            .ToList();
    }

    private static async Task SetPrivacyAsync(TestUser user, object privacy)
    {
        var res = await user.Client.PutAsJsonAsync($"/api/users/{user.Id}", new { Privacy = privacy });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OwnFeed_ShowsStartedFinishedAndReviewed()
    {
        var user = await _factory.CreateUserAsync("activity-own@test.com", "activityown");
        await user.Client.AddReviewAsync(await user.Client.TrackMovieAsync("Drifting Static", 100), 8);

        var kinds = (await ActivityAsync(user.Client, "?scope=mine")).Where(e => e.Title == "Drifting Static").Select(e => e.Kind);
        Assert.Equal(["finished", "reviewed", "started"], kinds.Order());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SharedFeed_ShowsFriendsActivity_AndNotStrangers(bool friends)
    {
        var viewer = await _factory.CreateUserAsync($"activity-viewer-{friends}@test.com", $"activityviewer{friends}");
        var other = await _factory.CreateUserAsync($"activity-other-{friends}@test.com", $"activityother{friends}");
        if (friends) await ApiHelpers.BefriendAsync(viewer, other);

        await other.Client.TrackMovieAsync($"Shared Feed Film {friends}", 50);

        var seen = (await ActivityAsync(viewer.Client, "?scope=all")).Any(e => e.Title == $"Shared Feed Film {friends}");
        Assert.Equal(friends, seen);
    }

    [Theory]
    [InlineData(true, "friends")]
    [InlineData(false, "none")]
    public async Task PublicProfile_ShowsCountsAndRelationship_AndActivityOnlyToFriends(bool friends, string relationship)
    {
        var viewer = await _factory.CreateUserAsync($"profile-viewer-{friends}@test.com", $"profileviewer{friends}");
        var target = await _factory.CreateUserAsync($"profile-target-{friends}@test.com", $"profiletarget{friends}");
        if (friends) await ApiHelpers.BefriendAsync(viewer, target);
        await target.Client.TrackMovieAsync($"Profile Film {friends}", 100);

        var profile = (await viewer.Client.GetFromJsonAsync<JsonElement>($"/api/users/{target.Id}")).GetProperty("data");
        Assert.Equal(relationship, profile.GetProperty("relationship").GetString());
        Assert.Equal(1, profile.GetProperty("counts").GetProperty("movie").GetInt32());

        var activity = await ActivityAsync(viewer.Client, $"?userId={target.Id}");
        Assert.Equal(friends, activity.Any(e => e.Title == $"Profile Film {friends}"));
    }

    [Fact]
    public async Task HidingMovieProgress_HidesOnlyThatFromFriends()
    {
        var viewer = await _factory.CreateUserAsync("privacy-progress-viewer@test.com", "privacyprogressviewer");
        var owner = await _factory.CreateUserAsync("privacy-progress-owner@test.com", "privacyprogressowner");
        await ApiHelpers.BefriendAsync(viewer, owner);
        await SetPrivacyAsync(owner, new { MovieProgress = false });

        await owner.Client.AddReviewAsync(await owner.Client.TrackMovieAsync("Private Progress Film", 100), 7);
        await owner.Client.PostAsJsonAsync("/api/tracking/books", new { Title = "Visible Progress Book", Pages = 100, Progress = 30 });

        var seen = await ActivityAsync(viewer.Client, "?scope=friends");
        Assert.DoesNotContain(seen, e => e.Title == "Private Progress Film" && e.Kind != "reviewed");
        Assert.Contains(seen, e => e.Title == "Private Progress Film" && e.Kind == "reviewed");
        Assert.Contains(seen, e => e.Title == "Visible Progress Book");
        Assert.Contains(await ActivityAsync(owner.Client, "?scope=mine"), e => e.Title == "Private Progress Film" && e.Kind == "finished");
    }

    [Fact]
    public async Task HidingMovieReviews_HidesOnlyThatFromFriends()
    {
        var viewer = await _factory.CreateUserAsync("privacy-review-viewer@test.com", "privacyreviewviewer");
        var owner = await _factory.CreateUserAsync("privacy-review-owner@test.com", "privacyreviewowner");
        await ApiHelpers.BefriendAsync(viewer, owner);
        await SetPrivacyAsync(owner, new { MovieReviews = false });

        await owner.Client.AddReviewAsync(await owner.Client.TrackMovieAsync("Private Review Film", 100), 6);

        var seen = await ActivityAsync(viewer.Client, "?scope=friends");
        Assert.DoesNotContain(seen, e => e.Title == "Private Review Film" && e.Kind == "reviewed");
        Assert.Contains(seen, e => e.Title == "Private Review Film" && e.Kind == "finished");
        Assert.Contains(await ActivityAsync(owner.Client, "?scope=mine"), e => e.Title == "Private Review Film" && e.Kind == "reviewed");
    }
}
