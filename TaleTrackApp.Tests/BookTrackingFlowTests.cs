using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Books as the KOReader plugin reports them: a book is stored once, matched first by ISBN, then
/// by title and author, then by title alone; Open Library completes what it lacks in the
/// background, at most once per book however often it syncs; and when Open Library fails the
/// progress is still recorded. Open Library answers from <see cref="FakeOpenLibrary"/>.
/// </summary>
[Collection(ApiCollection.Name)]
public class BookTrackingFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static async Task TrackBookAsync(HttpClient client, string title, int progress, string? author = null, string? isbn = null)
    {
        var res = await client.PostAsJsonAsync("/api/tracking/books",
            new { Title = title, Pages = 300, Progress = progress, Author = author, Isbn = isbn });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    private async Task<int> BookCountAsync(params string[] titles)
    {
        using var scope = _factory.NewDbScope(out var db);
        return await db.Medias.CountAsync(m => m.Type == Model.MediaType.Book && (titles.Contains(m.TitleEN!) || titles.Contains(m.TitleES!)));
    }

    [Fact]
    public async Task TrackBook_AppearsInTheLibrary_WithTheLatestProgress()
    {
        var user = await _factory.CreateUserAsync("book-track@test.com", "booktrack");

        await TrackBookAsync(user.Client, "Piranesi", 65, author: "Susanna Clarke");
        await TrackBookAsync(user.Client, "Piranesi", 40, author: "Susanna Clarke"); // the reader went back

        var library = await user.Client.LibraryAsync("?type=Book");
        Assert.Equal(1, library.GetProperty("count").GetInt32());
        var item = library.GetProperty("data")[0];
        Assert.Equal("Susanna Clarke", item.GetProperty("author").GetString());
        Assert.Equal(40, item.GetProperty("progress").GetInt32());
    }

    [Fact]
    public async Task SameIsbn_WithDifferentTitles_IsOneBook()
    {
        var user = await _factory.CreateUserAsync("book-isbn@test.com", "bookisbn");

        await TrackBookAsync(user.Client, "Dune", 20, isbn: "9780441013593");
        await TrackBookAsync(user.Client, "Dune (Spanish edition)", 30, isbn: "9780441013593");

        Assert.Equal(1, await BookCountAsync("Dune", "Dune (Spanish edition)"));
    }

    [Fact]
    public async Task SameTitleAndAuthor_WithoutIsbn_IsOneBook()
    {
        var owner = await _factory.CreateUserAsync("book-title-author-a@test.com", "booktitleauthora");
        var other = await _factory.CreateUserAsync("book-title-author-b@test.com", "booktitleauthorb");

        await TrackBookAsync(owner.Client, "El Nombre del Viento", 10, author: "Patrick Rothfuss");
        await TrackBookAsync(other.Client, "El Nombre del Viento", 90, author: "Patrick Rothfuss");

        Assert.Equal(1, await BookCountAsync("El Nombre del Viento"));
    }

    [Fact]
    public async Task TitleAlone_MatchesTheExistingBook()
    {
        var owner = await _factory.CreateUserAsync("book-title-a@test.com", "booktitlea");
        var other = await _factory.CreateUserAsync("book-title-b@test.com", "booktitleb");

        await TrackBookAsync(owner.Client, "The Left Hand of Darkness", 50, author: "Ursula K. Le Guin");
        await TrackBookAsync(other.Client, "The Left Hand of Darkness", 20);

        Assert.Equal(1, await BookCountAsync("The Left Hand of Darkness"));
    }

    [Fact]
    public async Task TrackBook_KnownToOpenLibrary_GetsCoverAuthorAndSynopsis()
    {
        _factory.OpenLibrary.Add(new FakeOpenLibrary.Entry("Enriched Book", "Known Author", 4242, "A book found on Open Library."));
        var user = await _factory.CreateUserAsync("book-enriched@test.com", "bookenriched");

        await TrackBookAsync(user.Client, "Enriched Book", 10);
        var mediaId = await user.Client.MediaIdAsync("Enriched Book");

        JsonElement detail = default;
        await Eventually.TrueAsync(async () =>
        {
            detail = await user.Client.MediaDetailAsync(mediaId);
            return detail.GetProperty("posterUrl").ValueKind != JsonValueKind.Null;
        }, "Open Library to fill in the cover");
        Assert.Contains("4242", detail.GetProperty("posterUrl").GetString());
        Assert.Equal("Known Author", detail.GetProperty("author").GetString());
        Assert.Equal("A book found on Open Library.", detail.GetProperty("description").GetString());
    }

    [Fact]
    public async Task IncompleteBook_SyncedAgainAndAgain_IsLookedUpOnlyOnce()
    {
        var user = await _factory.CreateUserAsync("book-throttle@test.com", "bookthrottle");

        await TrackBookAsync(user.Client, "Book Nobody Knows", 10);
        await Eventually.TrueAsync(() => Task.FromResult(_factory.OpenLibrary.SearchesFor("Book Nobody Knows") == 1),
            "the first Open Library search");

        await TrackBookAsync(user.Client, "Book Nobody Knows", 20);
        await TrackBookAsync(user.Client, "Book Nobody Knows", 30);
        await Task.Delay(300); // gives any further lookup time to happen

        Assert.Equal(1, _factory.OpenLibrary.SearchesFor("Book Nobody Knows"));
    }

    [Fact]
    public async Task TrackBook_WhileOpenLibraryFails_StillRecordsTheProgress()
    {
        _factory.OpenLibrary.AddDown("Book During Outage");
        var user = await _factory.CreateUserAsync("book-ol-down@test.com", "bookoldown");

        await TrackBookAsync(user.Client, "Book During Outage", 45);
        await Eventually.TrueAsync(() => Task.FromResult(_factory.OpenLibrary.SearchesFor("Book During Outage") == 1),
            "the failed Open Library search");

        var item = (await user.Client.LibraryAsync("?type=Book")).GetProperty("data")[0];
        Assert.Equal(45, item.GetProperty("progress").GetInt32());
    }
}
