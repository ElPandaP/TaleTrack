using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Movie and series progress as the browser extension reports it: a title shows up in the library,
/// each user keeps one entry per title where the latest report always wins (even going backwards),
/// a title is stored once however many users track it, and a series needs a valid season and episode.
/// </summary>
[Collection(ApiCollection.Name)]
public class TrackingFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static async Task TrackSeriesAsync(HttpClient client, string title, int season, int episode, int progress)
    {
        var res = await client.PostAsJsonAsync("/api/tracking/series",
            new { Title = title, Season = season, Episode = episode, Minutes = 45, Progress = progress });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    private async Task<int> MediaCountAsync(string title)
    {
        using var scope = _factory.NewDbScope(out var db);
        return await db.Medias.CountAsync(m => m.TitleEN == title || m.TitleES == title);
    }

    /// <summary>Tracking rows stored for a title, read from the database, to tell an update from a duplicate.</summary>
    private async Task<int> TrackingRowCountAsync(Guid userId, string title)
    {
        using var scope = _factory.NewDbScope(out var db);
        return await db.TrackingEvents.CountAsync(t => t.UserId == userId && (t.Media!.TitleEN == title || t.Media!.TitleES == title));
    }

    [Fact]
    public async Task TrackMovie_NewTitle_AppearsInTheLibraryWithItsProgress()
    {
        var user = await _factory.CreateUserAsync("track-new-movie@test.com", "tracknewmovie");

        await user.Client.TrackMovieAsync("The Endless Horizon", 10);

        var item = (await user.Client.LibraryAsync("?type=Movie")).GetProperty("data")[0];
        Assert.Equal("The Endless Horizon", item.GetProperty("titleEN").GetString());
        Assert.Equal(10, item.GetProperty("progress").GetInt32());
    }

    [Fact]
    public async Task TrackSeries_SeveralEpisodes_KeepOneEntryWithTheLatest()
    {
        var user = await _factory.CreateUserAsync("track-series@test.com", "trackseries");

        await TrackSeriesAsync(user.Client, "Severance", 1, 1, 100);
        await TrackSeriesAsync(user.Client, "Severance", 1, 2, 40);

        Assert.Equal(1, await TrackingRowCountAsync(user.Id, "Severance"));
        var item = (await user.Client.LibraryAsync("?type=Series")).GetProperty("data")[0];
        Assert.Equal(1, item.GetProperty("season").GetInt32());
        Assert.Equal(2, item.GetProperty("episode").GetInt32());
    }

    [Fact]
    public async Task TrackMovie_LowerProgress_StillReplacesTheStoredOne()
    {
        var user = await _factory.CreateUserAsync("track-movie-back@test.com", "trackmovieback");

        await user.Client.TrackMovieAsync("Arrival", 80);
        await user.Client.TrackMovieAsync("Arrival", 30);

        Assert.Equal(1, await TrackingRowCountAsync(user.Id, "Arrival"));
        Assert.Equal(30, (await user.Client.LibraryAsync("?type=Movie")).GetProperty("data")[0].GetProperty("progress").GetInt32());
    }

    [Fact]
    public async Task TrackSeries_EarlierEpisode_StillReplacesTheStoredOne()
    {
        var user = await _factory.CreateUserAsync("track-series-back@test.com", "trackseriesback");

        await TrackSeriesAsync(user.Client, "Fringe", 2, 5, 100);
        await TrackSeriesAsync(user.Client, "Fringe", 1, 3, 60);

        var item = (await user.Client.LibraryAsync("?type=Series")).GetProperty("data")[0];
        Assert.Equal(1, item.GetProperty("season").GetInt32());
        Assert.Equal(3, item.GetProperty("episode").GetInt32());
    }

    [Fact]
    public async Task TrackMovie_TwiceBySameUser_StoresTheTitleOnce()
    {
        var user = await _factory.CreateUserAsync("track-dedup-same@test.com", "trackdedupsame");

        await user.Client.TrackMovieAsync("Quiet Static", 15);
        await user.Client.TrackMovieAsync("Quiet Static", 60);

        Assert.Equal(1, await MediaCountAsync("Quiet Static"));
        Assert.Equal(1, (await user.Client.LibraryAsync("?type=Movie")).GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task TrackMovie_ByTwoUsers_SharesTheTitle_WithEachUsersOwnProgress()
    {
        var owner = await _factory.CreateUserAsync("track-dedup-a@test.com", "trackdedupa");
        var other = await _factory.CreateUserAsync("track-dedup-b@test.com", "trackdedupb");

        var ownerMediaId = await owner.Client.TrackMovieAsync("Nebula Drift", 100);
        var otherMediaId = await other.Client.TrackMovieAsync("Nebula Drift", 5);

        Assert.Equal(ownerMediaId, otherMediaId);
        Assert.Equal(1, await MediaCountAsync("Nebula Drift"));
        Assert.Equal(100, (await owner.Client.LibraryAsync()).GetProperty("data")[0].GetProperty("progress").GetInt32());
        Assert.Equal(5, (await other.Client.LibraryAsync()).GetProperty("data")[0].GetProperty("progress").GetInt32());
    }

    [Fact]
    public async Task MovieAndSeriesWithTheSameTitle_AreDifferentTitles()
    {
        var user = await _factory.CreateUserAsync("track-same-name@test.com", "tracksamename");

        await user.Client.TrackMovieAsync("Shared Name", 50);
        await TrackSeriesAsync(user.Client, "Shared Name", 1, 1, 50);

        Assert.Equal(2, await MediaCountAsync("Shared Name"));
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(1, null)]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public async Task TrackSeries_WithoutAValidSeasonAndEpisode_IsRejected(int? season, int? episode)
    {
        var user = await _factory.CreateUserAsync($"track-bad-ep-{season}-{episode}@test.com", $"trackbadep{season ?? 9}x{episode ?? 9}".Replace("-", "m"));

        var res = await user.Client.PostAsJsonAsync("/api/tracking/series",
            new { Title = "Zero Hour", Season = season, Episode = episode, Progress = 10 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
