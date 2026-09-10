using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

[Collection(ApiCollection.Name)]
public class BookTrackingFlowTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> RegisterAndLoginAsync(
        string email, string username, string password)
    {
        var registerRes = await _client.PostAsJsonAsync("/api/register",
            new { Email = email, Username = username, Password = password });

        Assert.True(registerRes.IsSuccessStatusCode,
            $"Register failed: {await registerRes.Content.ReadAsStringAsync()}");

        var loginRes = await _client.PostAsJsonAsync("/api/login",
            new { Email = email, Password = password });

        Assert.True(loginRes.IsSuccessStatusCode,
            $"Login failed: {await loginRes.Content.ReadAsStringAsync()}");

        var body = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean(), "Login response success=false");

        return body.GetProperty("token").GetString()!;
    }

    private void SetBearerToken(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    /// <summary>
    /// Full happy-path flow:
    ///   register → login → POST tracking (book) → GET /api/library?type=Book → 1 book with correct data
    /// </summary>
    [Fact]
    public async Task RegisterLoginTrackBook_AppearsInLibrary()
    {
        _client.DefaultRequestHeaders.Add("X-Internal-Api-Key", CustomWebApplicationFactory.TestInternalApiKey);

        var token = await RegisterAndLoginAsync("flow@test.com", "flowuser", "Password1!");
        SetBearerToken(token);

        var trackRes = await _client.PostAsJsonAsync("/api/tracking/books", new
        {
            Title    = "Dune",
            Pages    = 412,
            Progress = 100,
            Author   = "Frank Herbert",
            Isbn     = "9780441013593",
        });
        Assert.True(trackRes.IsSuccessStatusCode,
            $"Track failed: {await trackRes.Content.ReadAsStringAsync()}");

        var libraryRes = await _client.GetAsync("/api/library?type=Book");
        Assert.True(libraryRes.IsSuccessStatusCode,
            $"GET /api/library failed: {await libraryRes.Content.ReadAsStringAsync()}");

        var books = await libraryRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, books.GetProperty("count").GetInt32());

        var first = books.GetProperty("data")[0];
        Assert.Equal("Dune",          first.GetProperty("title").GetString());
        Assert.Equal("Frank Herbert", first.GetProperty("author").GetString());
    }

    /// <summary>Tracking the same book twice must NOT create a duplicate entry.</summary>
    [Fact]
    public async Task TrackSameBookTwice_OnlyOneEntryInLibrary()
    {
        _client.DefaultRequestHeaders.Add("X-Internal-Api-Key", CustomWebApplicationFactory.TestInternalApiKey);

        var token = await RegisterAndLoginAsync("dedup@test.com", "dedupuser", "Password1!");
        SetBearerToken(token);

        var payload = new
        {
            Title    = "El Nombre del Viento",
            Pages    = 662,
            Progress = 100,
            Author   = "Patrick Rothfuss",
        };

        await _client.PostAsJsonAsync("/api/tracking/books", payload);
        await _client.PostAsJsonAsync("/api/tracking/books", payload);

        var libraryRes = await _client.GetAsync("/api/library?type=Book");
        var books = await libraryRes.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, books.GetProperty("count").GetInt32());
    }

    /// <summary>
    /// The KOReader plugin sends reading progress repeatedly as you read.
    /// Progress must rise to the reported value but never go back down.
    /// </summary>
    [Fact]
    public async Task TrackBookProgress_RisesButNeverDrops()
    {
        _client.DefaultRequestHeaders.Add("X-Internal-Api-Key", CustomWebApplicationFactory.TestInternalApiKey);

        var token = await RegisterAndLoginAsync("progress@test.com", "progressuser", "Password1!");
        SetBearerToken(token);

        async Task<int?> ProgressAsync()
        {
            var lib = await (await _client.GetAsync("/api/library?type=Book")).Content
                .ReadFromJsonAsync<JsonElement>();
            var item = lib.GetProperty("data")[0];
            return item.GetProperty("progress").ValueKind == JsonValueKind.Null
                ? null
                : item.GetProperty("progress").GetInt32();
        }

        async Task PingAsync(int progress) =>
            await _client.PostAsJsonAsync("/api/tracking/books",
                new { Title = "Piranesi", Pages = 245, Progress = progress });

        await PingAsync(20);
        Assert.Equal(20, await ProgressAsync());

        await PingAsync(65);
        Assert.Equal(65, await ProgressAsync());

        await PingAsync(40);              // a stale ping arriving late
        Assert.Equal(65, await ProgressAsync());

        await PingAsync(100);
        Assert.Equal(100, await ProgressAsync());

        // still a single library entry throughout
        var final = await (await _client.GetAsync("/api/library?type=Book")).Content
            .ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, final.GetProperty("count").GetInt32());
    }

    /// <summary>A freshly logged-in user with no tracked books gets an empty list.</summary>
    [Fact]
    public async Task NewUser_BooksListIsEmpty()
    {
        _client.DefaultRequestHeaders.Add("X-Internal-Api-Key", CustomWebApplicationFactory.TestInternalApiKey);

        var token = await RegisterAndLoginAsync("empty@test.com", "emptyuser", "Password1!");
        SetBearerToken(token);

        var libraryRes = await _client.GetAsync("/api/library?type=Book");
        Assert.True(libraryRes.IsSuccessStatusCode);

        var books = await libraryRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, books.GetProperty("count").GetInt32());
    }

    /// <summary>GET /api/library without authentication must return 401.</summary>
    [Fact]
    public async Task GetLibrary_WithoutAuth_Returns401()
    {
        var client = factory.CreateClient(); // fresh client, no headers
        var res = await client.GetAsync("/api/library");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
