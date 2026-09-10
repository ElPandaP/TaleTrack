using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>The yearly consumption summary: totals by type, and the year filter.</summary>
[Collection(ApiCollection.Name)]
public class StatsFlowTests(CustomWebApplicationFactory factory)
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
    public async Task Stats_CountsTrackedItemsByType()
    {
        var client = await AuthedClientAsync("stats-counts@test.com", "statscounts");

        await client.PostAsJsonAsync("/api/tracking/movies", new { Title = "Counted Meteor", Minutes = 90, Progress = 100 });
        await client.PostAsJsonAsync("/api/tracking/books", new { Title = "Counted Almanac", Pages = 200, Progress = 100 });

        var res = await client.GetAsync("/api/stats");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var data = body.GetProperty("data");
        Assert.Equal(2, data.GetProperty("total").GetInt32());
        Assert.Equal(1, data.GetProperty("byType").GetProperty("movie").GetInt32());
        Assert.Equal(1, data.GetProperty("byType").GetProperty("book").GetInt32());
    }

    [Fact]
    public async Task Stats_YearFilter_ExcludesOtherYears()
    {
        var client = await AuthedClientAsync("stats-year@test.com", "statsyear");
        await client.PostAsJsonAsync("/api/tracking/movies", new { Title = "This Year Comet", Minutes = 90, Progress = 100 });

        var farFutureYear = DateTime.UtcNow.Year + 5;
        var res = await client.GetAsync($"/api/stats?year={farFutureYear}");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, body.GetProperty("data").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Stats_NewUser_IsAllZero()
    {
        var client = await AuthedClientAsync("stats-empty@test.com", "statsempty");

        var res = await client.GetAsync("/api/stats");
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, body.GetProperty("data").GetProperty("total").GetInt32());
    }
}
