using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Model;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Completing films and series with TMDB, which runs in the background after a progress report:
/// poster, synopsis, runtime, the title in the other language and, for a series, the episodes per
/// season, which then turn the latest episode watched into a percentage. TMDB answers from
/// <see cref="FakeTmdb"/>; when it fails, the progress is still recorded.
/// </summary>
[Collection(ApiCollection.Name)]
public class TmdbEnrichmentTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static readonly int[] TwoSeasonsOfTen = [10, 10];

    private static async Task<Guid> TrackSeriesAsync(HttpClient client, string title, int season, int episode)
    {
        var res = await client.PostAsJsonAsync("/api/tracking/series",
            new { Title = title, Season = season, Episode = episode, Progress = 0, Language = "en" });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return await client.MediaIdAsync(title);
    }

    /// <summary>Waits until the background enrichment has filled in <paramref name="field"/> of the media.</summary>
    private static async Task<JsonElement> WaitForEnrichmentAsync(HttpClient client, Guid mediaId, string field)
    {
        JsonElement detail = default;
        await Eventually.TrueAsync(async () =>
        {
            detail = await client.MediaDetailAsync(mediaId);
            return detail.GetProperty(field).ValueKind != JsonValueKind.Null;
        }, $"TMDB to fill in {field}");
        return detail;
    }

    [Fact]
    public async Task TrackMovie_KnownToTmdb_GetsPosterSynopsisRuntimeAndTheOtherTitle()
    {
        _factory.Tmdb.Add(new FakeTmdb.Entry("Enriched Film", "Película Enriquecida",
            Overview: "A film found on TMDB.", PosterPath: "/enriched.jpg", Runtime: 123));
        var user = await _factory.CreateUserAsync("tmdb-movie@test.com", "tmdbmovie");

        var res = await user.Client.PostAsJsonAsync("/api/tracking/movies", new { Title = "Enriched Film", Progress = 10, Language = "en" });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var detail = await WaitForEnrichmentAsync(user.Client, await user.Client.MediaIdAsync("Enriched Film"), "posterUrl");

        Assert.EndsWith("/enriched.jpg", detail.GetProperty("posterUrl").GetString());
        Assert.Equal("A film found on TMDB.", detail.GetProperty("description").GetString());
        Assert.Equal(123, detail.GetProperty("length").GetInt32());
        Assert.Equal("Película Enriquecida", detail.GetProperty("titleES").GetString());
    }

    [Fact]
    public async Task TrackSeries_KnownToTmdb_GetsItsEpisodesPerSeason()
    {
        _factory.Tmdb.Add(new FakeTmdb.Entry("Enriched Show", "Serie Enriquecida", SeasonEpisodeCounts: [8, 10, 6]));
        var user = await _factory.CreateUserAsync("tmdb-series@test.com", "tmdbseries");

        var mediaId = await TrackSeriesAsync(user.Client, "Enriched Show", 1, 1);
        var detail = await WaitForEnrichmentAsync(user.Client, mediaId, "seasonEpisodeCounts");

        Assert.Equal([8, 10, 6], detail.GetProperty("seasonEpisodeCounts").EnumerateArray().Select(c => c.GetInt32()));
    }

    [Theory]
    [InlineData(1, 5, 25)]
    [InlineData(1, 10, 50)]
    [InlineData(2, 1, 55)]
    [InlineData(2, 10, 100)]
    [InlineData(3, 4, 100)] // past the last known episode
    public async Task SeriesProgress_CountsEveryEpisodeUpToTheLatestWatched(int season, int episode, int expected)
    {
        var title = $"Progress Show S{season}E{episode}";
        _factory.Tmdb.Add(new FakeTmdb.Entry(title, $"Serie Progreso T{season}E{episode}", SeasonEpisodeCounts: TwoSeasonsOfTen));
        var user = await _factory.CreateUserAsync($"tmdb-progress-{season}-{episode}@test.com", $"tmdbprogress{season}{episode}");

        var mediaId = await TrackSeriesAsync(user.Client, title, season, episode);
        var detail = await WaitForEnrichmentAsync(user.Client, mediaId, "seasonEpisodeCounts");

        Assert.Equal(expected, detail.GetProperty("myProgress").GetInt32());
    }

    [Fact]
    public async Task SeriesProgress_WithoutEpisodeCounts_IsTheReportedProgress()
    {
        var user = await _factory.CreateUserAsync("tmdb-progress-unknown@test.com", "tmdbprogressunknown");

        var res = await user.Client.PostAsJsonAsync("/api/tracking/series",
            new { Title = "Unknown To Tmdb Show", Season = 1, Episode = 2, Progress = 40, Language = "en" });
        Assert.True(res.IsSuccessStatusCode);

        var detail = await user.Client.MediaDetailAsync(await user.Client.MediaIdAsync("Unknown To Tmdb Show"));
        Assert.Equal(40, detail.GetProperty("myProgress").GetInt32());
    }

    [Fact]
    public async Task TrackMovie_WhileTmdbFails_StillRecordsTheProgress()
    {
        _factory.Tmdb.AddDown("Film During Outage");
        var user = await _factory.CreateUserAsync("tmdb-down@test.com", "tmdbdown");

        var mediaId = await user.Client.TrackMovieAsync("Film During Outage", 35, language: "en");

        await Task.Delay(300); // gives the failed lookup time to finish
        var detail = await user.Client.MediaDetailAsync(mediaId);
        Assert.Equal(35, detail.GetProperty("myProgress").GetInt32());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("posterUrl").ValueKind);
    }

    [Fact]
    public async Task Enrichment_KeepsTheFirstSynopsis_AndClipsOneTooLong()
    {
        var user = await _factory.CreateUserAsync("tmdb-synopsis@test.com", "tmdbsynopsis");
        Guid keptId, clippedId;
        using (var scope = _factory.Services.CreateScope())
        {
            var media = scope.ServiceProvider.GetRequiredService<MediaService>();
            keptId = (await media.CreateAsync("Synopsis Kept Film", MediaType.Movie, length: 100)).Id;
            clippedId = (await media.CreateAsync("Synopsis Clipped Film", MediaType.Movie, length: 100)).Id;
            await media.ApplyTmdbEnrichmentAsync(keptId, new TmdbResult { Description = "First synopsis." });
            await media.ApplyTmdbEnrichmentAsync(keptId, new TmdbResult { Description = "Second synopsis." });
            await media.ApplyTmdbEnrichmentAsync(clippedId, new TmdbResult { Description = new string('x', 1500) });
        }

        Assert.Equal("First synopsis.", (await user.Client.MediaDetailAsync(keptId)).GetProperty("description").GetString());
        var clipped = (await user.Client.MediaDetailAsync(clippedId)).GetProperty("description").GetString()!;
        Assert.Equal(1000, clipped.Length); // what the database column holds
        Assert.EndsWith("…", clipped);
    }
}
