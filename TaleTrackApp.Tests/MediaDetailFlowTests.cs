using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Model;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// GET /api/media/{id}: the detail page combines the media, its reviews and the viewer's own
/// tracking and review.
/// </summary>
[Collection(ApiCollection.Name)]
public class MediaDetailFlowTests(CustomWebApplicationFactory factory)
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

    [Fact]
    public async Task GetMediaById_ReturnsMediaWithMyProgressAndMyReview()
    {
        var client = await AuthedClientAsync("detail@test.com", "detailuser");

        await client.PostAsJsonAsync("/api/tracking/movies",
            new { Title = "Detail Movie", Minutes = 100, Progress = 60 });
        var lib = await client.GetAsync("/api/library?type=Movie");
        var libBody = await lib.Content.ReadFromJsonAsync<JsonElement>();
        var mediaId = libBody.GetProperty("data").EnumerateArray()
            .First(i => i.GetProperty("titleEN").GetString() == "Detail Movie")
            .GetProperty("mediaId").GetGuid();

        var review = await client.PostAsJsonAsync("/api/reviews",
            new { MediaId = mediaId, Rating = 8, Comment = "Good" });
        Assert.True(review.IsSuccessStatusCode, await review.Content.ReadAsStringAsync());

        var res = await client.GetAsync($"/api/media/{mediaId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var data = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("Detail Movie", data.GetProperty("titleEN").GetString());
        Assert.Equal(60, data.GetProperty("myProgress").GetInt32());
        Assert.Equal(1, data.GetProperty("reviewCount").GetInt32());
        Assert.Equal(8, data.GetProperty("myRating").GetInt32());
        Assert.Equal(1, data.GetProperty("reviews").GetArrayLength());
    }

    /// <summary>Registers a media straight through the service and returns its id, as an enrichment would find it.</summary>
    private async Task<Guid> CreateMediaAsync(string title, MediaType type)
    {
        using var scope = _factory.Services.CreateScope();
        var media = await scope.ServiceProvider.GetRequiredService<MediaService>()
            .CreateAsync(title, type, length: 100);
        return media.Id;
    }

    private async Task<string?> DescriptionAsync(HttpClient client, Guid mediaId)
    {
        var res = await client.GetAsync($"/api/media/{mediaId}");
        var data = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        return data.GetProperty("description").GetString();
    }

    [Fact]
    public async Task GetMediaById_ShowsDescriptionFilledByTmdb_AndKeepsTheFirstOne()
    {
        var client = await AuthedClientAsync("desc-tmdb@test.com", "desctmdb");
        var id = await CreateMediaAsync("Description Movie", MediaType.Movie);
        Assert.Null(await DescriptionAsync(client, id));

        using (var scope = _factory.Services.CreateScope())
        {
            var media = scope.ServiceProvider.GetRequiredService<MediaService>();
            await media.ApplyTmdbEnrichmentAsync(id, new TmdbResult { Description = "First synopsis." });
            await media.ApplyTmdbEnrichmentAsync(id, new TmdbResult { Description = "Second synopsis." });
        }

        Assert.Equal("First synopsis.", await DescriptionAsync(client, id));
    }

    [Fact]
    public async Task GetMediaById_ShowsDescriptionFilledByOpenLibrary()
    {
        var client = await AuthedClientAsync("desc-ol@test.com", "descol");
        var id = await CreateMediaAsync("Description Book", MediaType.Book);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<MediaService>()
                .ApplyEnrichmentAsync(id, new OpenLibraryResult { Description = "  A book synopsis.  " });
        }

        Assert.Equal("A book synopsis.", await DescriptionAsync(client, id));
    }

    [Fact]
    public async Task Enrichment_ClipsALongDescriptionToWhatTheColumnHolds()
    {
        var client = await AuthedClientAsync("desc-long@test.com", "desclong");
        var id = await CreateMediaAsync("Long Description Movie", MediaType.Movie);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<MediaService>()
                .ApplyTmdbEnrichmentAsync(id, new TmdbResult { Description = new string('x', 1500) });
        }

        var description = await DescriptionAsync(client, id);
        Assert.NotNull(description);
        Assert.Equal(1000, description!.Length);
        Assert.EndsWith("…", description);
    }

    [Fact]
    public async Task GetMediaById_UnknownId_Returns404()
    {
        var client = await AuthedClientAsync("detail404@test.com", "detail404user");

        var res = await client.GetAsync($"/api/media/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
