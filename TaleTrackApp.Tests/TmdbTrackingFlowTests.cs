using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Covers the movie/series tracking flow end to end: new content, content already
/// registered by someone else, content the user already has, and the library
/// "edit progress" endpoint (including editing something the user doesn't have).
/// TMDB enrichment itself is stubbed to always miss (see CustomWebApplicationFactory) —
/// these tests are about the tracking/dedup/library behaviour around it, not about
/// parsing real TMDB responses.
/// </summary>
[Collection(ApiCollection.Name)]
public class TmdbTrackingFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private async Task<HttpClient> AuthedClientAsync(string email, string username)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", CustomWebApplicationFactory.TestInternalApiKey);

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

    private async Task<JsonElement> AllMediaNamed(string title)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", CustomWebApplicationFactory.TestInternalApiKey);
        var res = await client.GetAsync("/api/media");
        Assert.True(res.IsSuccessStatusCode, $"GET /api/media failed: {await res.Content.ReadAsStringAsync()}");
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return JsonSerializer.SerializeToElement(
            body.GetProperty("data").EnumerateArray().Where(m => m.GetProperty("title").GetString() == title));
    }

    // ─── 1. New movie/series, not registered yet ──────────────────────────

    [Fact]
    public async Task TrackMovie_NewTitle_CreatesOneMediaRow_AppearsInLibrary()
    {
        var client = await AuthedClientAsync("tmdb-new-movie@test.com", "tmdbnewmovie");

        var res = await client.PostAsJsonAsync("/api/tracking/movies",
            new { Title = "The Endless Horizon", Minutes = 118, Progress = 10, Language = "es" });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        var lib = await LibraryRows(client, "Movie");
        Assert.Equal(1, lib.GetProperty("count").GetInt32());
        var item = lib.GetProperty("data")[0];
        Assert.Equal("The Endless Horizon", item.GetProperty("title").GetString());
        Assert.Equal(10, item.GetProperty("progress").GetInt32());

        // exactly one Media row was created for it — TMDB enrichment (stubbed to
        // miss) must not have spun up a second/duplicate row
        var rows = await AllMediaNamed("The Endless Horizon");
        Assert.Single(rows.EnumerateArray());
    }

    [Fact]
    public async Task TrackSeries_NewTitle_CreatesOneMediaRow_AppearsInLibrary()
    {
        var client = await AuthedClientAsync("tmdb-new-series@test.com", "tmdbnewseries");

        var res = await client.PostAsJsonAsync("/api/tracking/series",
            new { Title = "Static Frontier", Season = 1, Episode = 1, Minutes = 45, Progress = 20, Language = "en" });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        var lib = await LibraryRows(client, "Series");
        Assert.Equal(1, lib.GetProperty("count").GetInt32());

        var rows = await AllMediaNamed("Static Frontier");
        Assert.Single(rows.EnumerateArray());
    }

    // ─── 2. Registered (by someone else), but this user doesn't have it ──

    [Fact]
    public async Task TrackMovie_AlreadyRegisteredByAnotherUser_ReusesMediaRow_NotDuplicated()
    {
        var owner = await AuthedClientAsync("tmdb-shared-owner@test.com", "tmdbsharedowner");
        await owner.PostAsJsonAsync("/api/tracking/movies",
            new { Title = "Nebula Drift", Minutes = 132, Progress = 100, Language = "es" });

        var other = await AuthedClientAsync("tmdb-shared-other@test.com", "tmdbsharedother");
        var res = await other.PostAsJsonAsync("/api/tracking/movies",
            new { Title = "Nebula Drift", Minutes = 132, Progress = 5, Language = "es" });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        // still exactly one Media row for it, globally
        var rows = await AllMediaNamed("Nebula Drift");
        Assert.Single(rows.EnumerateArray());

        // both users see the SAME mediaId, but each their own progress
        var ownerLib = await LibraryRows(owner, "Movie");
        var otherLib = await LibraryRows(other, "Movie");
        var ownerItem = ownerLib.GetProperty("data").EnumerateArray()
            .First(i => i.GetProperty("title").GetString() == "Nebula Drift");
        var otherItem = otherLib.GetProperty("data").EnumerateArray()
            .First(i => i.GetProperty("title").GetString() == "Nebula Drift");

        Assert.Equal(ownerItem.GetProperty("mediaId").GetInt32(), otherItem.GetProperty("mediaId").GetInt32());
        Assert.Equal(100, ownerItem.GetProperty("progress").GetInt32());
        Assert.Equal(5, otherItem.GetProperty("progress").GetInt32());
    }

    // ─── 3. Registered AND this user already has it (re-track) ───────────

    [Fact]
    public async Task TrackMovie_AlreadyTrackedByThisUser_UpsertsInPlace_NoDuplicate()
    {
        var client = await AuthedClientAsync("tmdb-retrack@test.com", "tmdbretrack");

        await client.PostAsJsonAsync("/api/tracking/movies",
            new { Title = "Quiet Static", Minutes = 101, Progress = 15, Language = "en" });
        var firstLib = await LibraryRows(client, "Movie");
        var mediaId = firstLib.GetProperty("data")[0].GetProperty("mediaId").GetInt32();

        var res = await client.PostAsJsonAsync("/api/tracking/movies",
            new { Title = "Quiet Static", Minutes = 101, Progress = 60, Language = "en" });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        var secondLib = await LibraryRows(client, "Movie");
        Assert.Equal(1, secondLib.GetProperty("count").GetInt32()); // no duplicate row
        var item = secondLib.GetProperty("data")[0];
        Assert.Equal(mediaId, item.GetProperty("mediaId").GetInt32()); // same media
        Assert.Equal(60, item.GetProperty("progress").GetInt32());     // progress updated

        var rows = await AllMediaNamed("Quiet Static");
        Assert.Single(rows.EnumerateArray());
    }

    // ─── 4. Edit progress (library "edit progress" action) ───────────────

    [Fact]
    public async Task EditTrackingProgress_ExistingTracking_UpdatesProgress()
    {
        var client = await AuthedClientAsync("tmdb-edit-progress@test.com", "tmdbeditprogress");

        await client.PostAsJsonAsync("/api/tracking/movies",
            new { Title = "Paper Lanterns", Minutes = 95, Progress = 20 });
        var lib = await LibraryRows(client, "Movie");
        var mediaId = lib.GetProperty("data")[0].GetProperty("mediaId").GetInt32();

        var res = await client.PutAsJsonAsync($"/api/tracking/{mediaId}", new { Progress = 55 });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(55, body.GetProperty("data").GetProperty("progress").GetInt32());

        var libAfter = await LibraryRows(client, "Movie");
        Assert.Equal(55, libAfter.GetProperty("data")[0].GetProperty("progress").GetInt32());
    }

    // ─── 5. Edit progress on something not registered / not owned ────────

    [Fact]
    public async Task EditTrackingProgress_MediaIdDoesNotExist_Returns404()
    {
        var client = await AuthedClientAsync("tmdb-edit-404-unknown@test.com", "tmdbedit404unknown");

        var res = await client.PutAsJsonAsync("/api/tracking/999999999", new { Progress = 50 });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task EditTrackingProgress_MediaExistsButNotTrackedByThisUser_Returns404_AndDoesNotTouchOwner()
    {
        var owner = await AuthedClientAsync("tmdb-edit-404-owner@test.com", "tmdbedit404owner");
        await owner.PostAsJsonAsync("/api/tracking/series",
            new { Title = "Hollow Signal", Season = 1, Episode = 1, Minutes = 40, Progress = 30 });
        var ownerLib = await LibraryRows(owner, "Series");
        var mediaId = ownerLib.GetProperty("data")[0].GetProperty("mediaId").GetInt32();

        var stranger = await AuthedClientAsync("tmdb-edit-404-stranger@test.com", "tmdbedit404stranger");
        var res = await stranger.PutAsJsonAsync($"/api/tracking/{mediaId}", new { Progress = 90 });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

        // owner's own progress must be untouched by the stranger's failed attempt
        var ownerLibAfter = await LibraryRows(owner, "Series");
        Assert.Equal(30, ownerLibAfter.GetProperty("data")[0].GetProperty("progress").GetInt32());
    }
}
