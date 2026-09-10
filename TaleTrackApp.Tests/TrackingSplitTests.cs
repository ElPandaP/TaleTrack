using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaleTrackApp.Data;
using Xunit;

namespace TaleTrackApp.Tests;

[Collection(ApiCollection.Name)]
public class TrackingSplitTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private async Task<HttpClient> AuthedClientAsync(string email, string username)
    {
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/register",
            new { Email = email, Username = username, Password = "Password1!" });
        var login = await client.PostAsJsonAsync("/api/login",
            new { Email = email, Password = "Password1!" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return client;
    }

    private static async Task<JsonElement> LibraryRows(HttpClient client, string? type = null)
    {
        var url = type is null ? "/api/library" : $"/api/library?type={type}";
        var res = await client.GetAsync(url);
        Assert.True(res.IsSuccessStatusCode, $"GET {url} failed: {await res.Content.ReadAsStringAsync()}");
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Raw TrackingEvent row count for a title, read straight from the DB — the only way
    /// left to tell "upserted in place" apart from "a duplicate row happened to collapse
    /// the same in the library view", now that there's no GET /api/tracking to inspect.
    /// </summary>
    private async Task<int> RawEventCountAsync(string title)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TrackingEvents.CountAsync(te => te.Media!.Title == title);
    }

    [Fact]
    public async Task TrackMovie_Upserts_LatestProgressWins()
    {
        var client = await AuthedClientAsync("movie-upsert@test.com", "movieupsert");

        foreach (var p in new[] { 30, 70 })
        {
            var res = await client.PostAsJsonAsync("/api/tracking/movies",
                new { Title = "Blade Runner 2049", Minutes = 164, Progress = p });
            Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        }

        Assert.Equal(1, await RawEventCountAsync("Blade Runner 2049"));

        var lib = await LibraryRows(client, "Movie");
        Assert.Equal(70, lib.GetProperty("data")[0].GetProperty("progress").GetInt32());
    }

    [Fact]
    public async Task TrackMovie_ProgressNeverGoesDown()
    {
        var client = await AuthedClientAsync("movie-monotonic@test.com", "moviemono");

        await client.PostAsJsonAsync("/api/tracking/movies", new { Title = "Arrival", Minutes = 116, Progress = 80 });
        await client.PostAsJsonAsync("/api/tracking/movies", new { Title = "Arrival", Minutes = 116, Progress = 25 });

        Assert.Equal(1, await RawEventCountAsync("Arrival"));

        var lib = await LibraryRows(client, "Movie");
        Assert.Equal(80, lib.GetProperty("data")[0].GetProperty("progress").GetInt32());
    }

    [Fact]
    public async Task TrackSeries_DistinctEpisodes_AreSeparateRows_LibraryCollapses()
    {
        var client = await AuthedClientAsync("series-eps@test.com", "serieseps");

        await client.PostAsJsonAsync("/api/tracking/series",
            new { Title = "Severance", Season = 1, Episode = 1, EpisodeTitle = "Good News About Hell", Minutes = 57, Progress = 100 });
        await client.PostAsJsonAsync("/api/tracking/series",
            new { Title = "Severance", Season = 1, Episode = 2, Minutes = 49, Progress = 40 });
        // re-report episode 2 — must upsert, not add
        await client.PostAsJsonAsync("/api/tracking/series",
            new { Title = "Severance", Season = 1, Episode = 2, Minutes = 49, Progress = 75 });

        // One row per episode (2), not one per POST (3) — episode 2's re-report upserted.
        Assert.Equal(2, await RawEventCountAsync("Severance"));

        var lib = await LibraryRows(client, "Series");
        Assert.Equal(1, lib.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task TrackSeries_MissingSeasonOrEpisode_Returns400()
    {
        var client = await AuthedClientAsync("series-validate@test.com", "seriesval");

        var res = await client.PostAsJsonAsync("/api/tracking/series",
            new { Title = "Dark", Minutes = 55, Progress = 10 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task OldTrackingPostRoute_IsGone()
    {
        var client = await AuthedClientAsync("old-route@test.com", "oldroute");
        // The pre-split /api/tracking route (POST) no longer exists at all — not even for GET.
        var res = await client.PostAsJsonAsync("/api/tracking",
            new { Title = "X", Type = "Movie", Length = 100, Progress = 10 });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
