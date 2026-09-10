using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>Library filters: by type and by status (in-progress vs finished).</summary>
[Collection(ApiCollection.Name)]
public class LibraryFlowTests(CustomWebApplicationFactory factory)
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

    private static async Task<JsonElement> LibraryAsync(HttpClient client, string query)
    {
        var res = await client.GetAsync($"/api/library{query}");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task TypeFilter_OnlyReturnsThatType()
    {
        var client = await AuthedClientAsync("library-type@test.com", "librarytype");

        await client.PostAsJsonAsync("/api/tracking/movies", new { Title = "Glass Tundra", Minutes = 90, Progress = 10 });
        await client.PostAsJsonAsync("/api/tracking/books", new { Title = "The Last Cartographer", Pages = 300, Progress = 10 });

        var movies = await LibraryAsync(client, "?type=Movie");
        Assert.Equal(1, movies.GetProperty("count").GetInt32());
        Assert.Equal("Movie", movies.GetProperty("data")[0].GetProperty("type").GetString());

        var books = await LibraryAsync(client, "?type=Book");
        Assert.Equal(1, books.GetProperty("count").GetInt32());
        Assert.Equal("Book", books.GetProperty("data")[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task StatusFilter_SplitsInProgressAndFinished()
    {
        var client = await AuthedClientAsync("library-status@test.com", "librarystatus");

        await client.PostAsJsonAsync("/api/tracking/movies", new { Title = "Amber Static", Minutes = 90, Progress = 40 });
        await client.PostAsJsonAsync("/api/tracking/movies", new { Title = "Rusted Compass", Minutes = 90, Progress = 100 });

        var inProgress = await LibraryAsync(client, "?type=Movie&status=in_progress");
        Assert.Equal(1, inProgress.GetProperty("count").GetInt32());
        Assert.Equal("Amber Static", inProgress.GetProperty("data")[0].GetProperty("title").GetString());

        var finished = await LibraryAsync(client, "?type=Movie&status=finished");
        Assert.Equal(1, finished.GetProperty("count").GetInt32());
        Assert.Equal("Rusted Compass", finished.GetProperty("data")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Library_OnlyShowsTheCallingUsersOwnItems()
    {
        var a = await AuthedClientAsync("library-scope-a@test.com", "libraryscopea");
        var b = await AuthedClientAsync("library-scope-b@test.com", "libraryscopeb");

        await a.PostAsJsonAsync("/api/tracking/movies", new { Title = "Private Nebula", Minutes = 90, Progress = 20 });

        var bLibrary = await LibraryAsync(b, "?type=Movie");
        Assert.DoesNotContain(bLibrary.GetProperty("data").EnumerateArray(),
            i => i.GetProperty("title").GetString() == "Private Nebula");
    }
}
