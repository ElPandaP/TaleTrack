using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// The library and the media detail page: filtering by type, status and year, sorting, each user
/// only seeing their own items, the detail with the user's progress and everyone's reviews, and
/// correcting or removing a title by hand.
/// </summary>
[Collection(ApiCollection.Name)]
public class LibraryFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static List<string?> Titles(JsonElement library) =>
        library.GetProperty("data").EnumerateArray().Select(i => i.GetProperty("titleEN").GetString()).ToList();

    /// <summary>Moves the user's latest activity on a media to another year.</summary>
    private async Task MoveActivityToYearAsync(Guid userId, Guid mediaId, int year)
    {
        using var scope = _factory.NewDbScope(out var db);
        var tracking = await db.TrackingEvents.SingleAsync(t => t.UserId == userId && t.MediaId == mediaId);
        tracking.EventDate = new DateTime(year, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Filters_ByTypeStatusAndYear_ReturnOnlyWhatMatches()
    {
        var user = await _factory.CreateUserAsync("library-filters@test.com", "libraryfilters");
        await user.Client.TrackMovieAsync("Amber Static", 40);
        await user.Client.TrackMovieAsync("Rusted Compass", 100);
        var oldId = await user.Client.TrackMovieAsync("Last Year Film", 100);
        await MoveActivityToYearAsync(user.Id, oldId, DateTime.UtcNow.Year - 1);
        await user.Client.PostAsJsonAsync("/api/tracking/books", new { Title = "The Last Cartographer", Pages = 300, Progress = 10 });

        Assert.Equal(["The Last Cartographer"], Titles(await user.Client.LibraryAsync("?type=Book")));
        Assert.Equal(["Amber Static"], Titles(await user.Client.LibraryAsync("?type=Movie&status=in_progress")));
        Assert.Equal(["Rusted Compass", "Last Year Film"], Titles(await user.Client.LibraryAsync("?type=Movie&status=finished")));
        Assert.Equal(["Last Year Film"], Titles(await user.Client.LibraryAsync($"?year={DateTime.UtcNow.Year - 1}")));
    }

    [Fact]
    public async Task SortByRating_PutsTheHighestRatedFirst()
    {
        var user = await _factory.CreateUserAsync("library-sort@test.com", "librarysort");
        await user.Client.AddReviewAsync(await user.Client.TrackMovieAsync("Rated Six", 100), 6);
        await user.Client.AddReviewAsync(await user.Client.TrackMovieAsync("Rated Nine", 100), 9);
        await user.Client.TrackMovieAsync("Not Rated", 100);

        Assert.Equal(["Rated Nine", "Rated Six", "Not Rated"], Titles(await user.Client.LibraryAsync("?sort=rating")));
    }

    [Fact]
    public async Task Library_OnlyShowsTheCallersOwnItems()
    {
        var a = await _factory.CreateUserAsync("library-scope-a@test.com", "libraryscopea");
        var b = await _factory.CreateUserAsync("library-scope-b@test.com", "libraryscopeb");

        await a.Client.TrackMovieAsync("Private Nebula", 20);

        Assert.DoesNotContain("Private Nebula", Titles(await b.Client.LibraryAsync()));
    }

    [Fact]
    public async Task MediaDetail_ShowsMyProgressAndReview_AndEveryonesReviews()
    {
        var me = await _factory.CreateUserAsync("detail-me@test.com", "detailme");
        var mediaId = await me.Client.TrackMovieAsync("Detail Movie", 60);
        await me.Client.AddReviewAsync(mediaId, 8, "Good");
        foreach (var name in new[] { "detailother1", "detailother2" })
        {
            var other = await _factory.CreateUserAsync($"{name}@test.com", name);
            await other.Client.TrackMovieAsync("Detail Movie", 100);
            await other.Client.AddReviewAsync(mediaId, 5);
        }

        var detail = await me.Client.MediaDetailAsync(mediaId);
        Assert.Equal(60, detail.GetProperty("myProgress").GetInt32());
        Assert.Equal(8, detail.GetProperty("myRating").GetInt32());
        Assert.Equal(3, detail.GetProperty("reviewCount").GetInt32());
        Assert.Equal(3, detail.GetProperty("reviews").GetArrayLength());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(55)]
    [InlineData(100)]
    public async Task EditProgress_WithinRange_IsSaved(int progress)
    {
        var user = await _factory.CreateUserAsync($"edit-progress-{progress}@test.com", $"editprogress{progress}");
        var mediaId = await user.Client.TrackMovieAsync("Paper Lanterns", 20);

        var res = await user.Client.PutAsJsonAsync($"/api/tracking/{mediaId}", new { Progress = progress });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        Assert.Equal(progress, (await user.Client.MediaDetailAsync(mediaId)).GetProperty("myProgress").GetInt32());
        // 100 % is what makes a title finished.
        Assert.Equal(progress == 100, Titles(await user.Client.LibraryAsync("?status=finished")).Contains("Paper Lanterns"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task EditProgress_OutOfRange_IsRejected(int progress)
    {
        var user = await _factory.CreateUserAsync($"edit-progress-bad{progress}@test.com", $"editprogressbad{progress + 10}");
        var mediaId = await user.Client.TrackMovieAsync("Out Of Range Film", 20);

        var res = await user.Client.PutAsJsonAsync($"/api/tracking/{mediaId}", new { Progress = progress });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task EditEpisode_OfASeries_IsSaved()
    {
        var user = await _factory.CreateUserAsync("edit-episode@test.com", "editepisode");
        await user.Client.PostAsJsonAsync("/api/tracking/series", new { Title = "Lantern Tide", Season = 1, Episode = 1, Progress = 30 });
        var mediaId = await user.Client.MediaIdAsync("Lantern Tide");

        var res = await user.Client.PutAsJsonAsync($"/api/tracking/{mediaId}", new { Season = 2, Episode = 3 });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        var detail = await user.Client.MediaDetailAsync(mediaId);
        Assert.Equal(2, detail.GetProperty("mySeason").GetInt32());
        Assert.Equal(3, detail.GetProperty("myEpisode").GetInt32());
    }

    [Fact]
    public async Task EditProgress_OfATitleTheUserDoesNotTrack_IsRejected_AndCreatesNothing()
    {
        var owner = await _factory.CreateUserAsync("edit-stranger-owner@test.com", "editstrangerowner");
        var stranger = await _factory.CreateUserAsync("edit-stranger@test.com", "editstranger");
        var mediaId = await owner.Client.TrackMovieAsync("Hollow Signal", 30);

        var res = await stranger.Client.PutAsJsonAsync($"/api/tracking/{mediaId}", new { Progress = 90 });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

        Assert.Equal(0, (await stranger.Client.LibraryAsync()).GetProperty("count").GetInt32());
        Assert.Equal(30, (await owner.Client.MediaDetailAsync(mediaId)).GetProperty("myProgress").GetInt32());
    }

    [Fact]
    public async Task RemoveFromLibrary_TakesItOutOfTheLibraryAndTheStats()
    {
        var user = await _factory.CreateUserAsync("library-remove@test.com", "libraryremove");
        var mediaId = await user.Client.TrackMovieAsync("Ashen Meridian", 100);

        var res = await user.Client.DeleteAsync($"/api/tracking/{mediaId}");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        Assert.Equal(0, (await user.Client.LibraryAsync()).GetProperty("count").GetInt32());
        var stats = await user.Client.GetFromJsonAsync<JsonElement>("/api/stats");
        Assert.Equal(0, stats.GetProperty("data").GetProperty("total").GetInt32());
    }
}
