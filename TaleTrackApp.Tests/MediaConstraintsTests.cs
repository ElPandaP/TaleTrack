using Microsoft.EntityFrameworkCore;
using TaleTrackApp.Model;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// The database itself rejects media rows that fill in columns belonging to another type
/// (for example an ISBN on a movie) or that have an unknown type, and accepts each type with its
/// own columns.
/// </summary>
[Collection(ApiCollection.Name)]
public class MediaConstraintsTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    /// <summary>
    /// The rows under test, by name. The theories receive the name rather than the row so that the
    /// test list shows each case separately.
    /// </summary>
    private static readonly Dictionary<string, Func<Media>> Rows = new()
    {
        ["movie with ISBN"] = () => new Media { TitleEN = "Constraint Movie Isbn", Type = MediaType.Movie, Isbn = "9780000000001" },
        ["series with author"] = () => new Media { TitleEN = "Constraint Series Author", Type = MediaType.Series, Author = "Someone" },
        ["book with seasons"] = () => new Media { TitleEN = "Constraint Book Seasons", Type = MediaType.Book, SeasonEpisodeCounts = [10, 8] },
        ["unknown type"] = () => new Media { TitleEN = "Constraint Bad Type", Type = (MediaType)99 },
        ["book"] = () => new Media { TitleEN = "Constraint Ok Book", Type = MediaType.Book, Author = "A", Isbn = "9780000000002" },
        ["series"] = () => new Media { TitleEN = "Constraint Ok Series", Type = MediaType.Series, SeasonEpisodeCounts = [10, 8] },
        ["movie"] = () => new Media { TitleEN = "Constraint Ok Movie", Type = MediaType.Movie, Length = 100 },
    };

    private async Task<bool> IsRejectedAsync(string row)
    {
        using var scope = _factory.NewDbScope(out var db);
        db.Medias.Add(Rows[row]());
        try
        {
            await db.SaveChangesAsync();
            return false;
        }
        catch (DbUpdateException)
        {
            return true;
        }
    }

    [Theory]
    [InlineData("movie with ISBN")]
    [InlineData("series with author")]
    [InlineData("book with seasons")]
    [InlineData("unknown type")]
    public async Task MediaWithAnotherTypesColumns_IsRejected(string row) =>
        Assert.True(await IsRejectedAsync(row));

    [Theory]
    [InlineData("book")]
    [InlineData("series")]
    [InlineData("movie")]
    public async Task MediaWithItsOwnColumns_IsAccepted(string row) =>
        Assert.False(await IsRejectedAsync(row));
}
