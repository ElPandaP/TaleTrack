using Microsoft.EntityFrameworkCore;
using TaleTrackApp.Model;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>The database itself rejects media rows whose type-specific columns don't match their type.</summary>
[Collection(ApiCollection.Name)]
public class MediaConstraintsTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private async Task<bool> SaveIsRejectedAsync(Media media)
    {
        using var scope = _factory.NewDbScope(out var db);
        db.Medias.Add(media);
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

    [Fact]
    public async Task Movie_WithIsbn_IsRejected() =>
        Assert.True(await SaveIsRejectedAsync(
            new Media { TitleEN = "Constraint Movie Isbn", Type = MediaType.Movie, Isbn = "9780000000001" }));

    [Fact]
    public async Task Series_WithAuthor_IsRejected() =>
        Assert.True(await SaveIsRejectedAsync(
            new Media { TitleEN = "Constraint Series Author", Type = MediaType.Series, Author = "Someone" }));

    [Fact]
    public async Task Book_WithSeasonEpisodeCounts_IsRejected() =>
        Assert.True(await SaveIsRejectedAsync(
            new Media { TitleEN = "Constraint Book Seasons", Type = MediaType.Book, SeasonEpisodeCounts = [10, 8] }));

    [Fact]
    public async Task UndefinedType_IsRejected() =>
        Assert.True(await SaveIsRejectedAsync(
            new Media { TitleEN = "Constraint Bad Type", Type = (MediaType)99 }));

    [Fact]
    public async Task EachTypeWithItsOwnColumns_IsAccepted()
    {
        Assert.False(await SaveIsRejectedAsync(
            new Media { TitleEN = "Constraint Ok Book", Type = MediaType.Book, Author = "A", Isbn = "9780000000002" }));
        Assert.False(await SaveIsRejectedAsync(
            new Media { TitleEN = "Constraint Ok Series", Type = MediaType.Series, SeasonEpisodeCounts = [10, 8] }));
        Assert.False(await SaveIsRejectedAsync(
            new Media { TitleEN = "Constraint Ok Movie", Type = MediaType.Movie, Length = 100 }));
    }
}
