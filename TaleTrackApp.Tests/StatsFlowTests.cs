using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>The yearly consumption summary: titles with activity in the year asked for, counted by type.</summary>
[Collection(ApiCollection.Name)]
public class StatsFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Stats_CountOnlyTheYearAskedFor_ByType()
    {
        var user = await _factory.CreateUserAsync("stats-year@test.com", "statsyear");
        await user.Client.TrackMovieAsync("Counted Meteor", 100);
        await user.Client.PostAsJsonAsync("/api/tracking/books", new { Title = "Counted Almanac", Pages = 200, Progress = 100 });
        var oldId = await user.Client.TrackMovieAsync("Last Year Meteor", 100);
        var lastYear = DateTime.UtcNow.Year - 1;
        using (_factory.NewDbScope(out var db))
        {
            var tracking = await db.TrackingEvents.SingleAsync(t => t.UserId == user.Id && t.MediaId == oldId);
            tracking.EventDate = new DateTime(lastYear, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            await db.SaveChangesAsync();
        }

        var thisYear = (await user.Client.GetFromJsonAsync<JsonElement>($"/api/stats?year={DateTime.UtcNow.Year}")).GetProperty("data");
        Assert.Equal(2, thisYear.GetProperty("total").GetInt32());
        Assert.Equal(1, thisYear.GetProperty("byType").GetProperty("movie").GetInt32());
        Assert.Equal(1, thisYear.GetProperty("byType").GetProperty("book").GetInt32());

        var previous = (await user.Client.GetFromJsonAsync<JsonElement>($"/api/stats?year={lastYear}")).GetProperty("data");
        Assert.Equal(1, previous.GetProperty("total").GetInt32());
        Assert.Equal(1, previous.GetProperty("byType").GetProperty("movie").GetInt32());
    }
}
