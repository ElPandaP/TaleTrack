using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

    [Fact]
    public async Task GetMediaById_UnknownId_Returns404()
    {
        var client = await AuthedClientAsync("detail404@test.com", "detail404user");

        var res = await client.GetAsync($"/api/media/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
