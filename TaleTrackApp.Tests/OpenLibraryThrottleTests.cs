using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TaleTrackApp.Data;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Model;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// A book that is still incomplete asks for Open Library enrichment on every sync; the lookup must
/// not go out each time. The HTTP handler here only counts requests, nothing leaves the process.
/// </summary>
[Collection(ApiCollection.Name)]
public class OpenLibraryThrottleTests(CustomWebApplicationFactory factory)
{
    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Requests;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Requests);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }
    }

    [Fact]
    public async Task EnrichFromOpenLibrary_LooksAnUnresolvedBookUpOnlyOnce()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var handler = new CountingHandler();
        var config = new ConfigurationBuilder().Build();
        var media = new MediaService(
            db,
            new TmdbService(new HttpClient(new CountingHandler()), NullLogger<TmdbService>.Instance, config),
            new OpenLibraryService(new HttpClient(handler), NullLogger<OpenLibraryService>.Instance, config),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<MediaService>.Instance);

        var book = await media.CreateAsync("Throttled Book", MediaType.Book, length: 200);

        await media.EnrichFromOpenLibraryAsync(book.Id, "Throttled Book", null, null);
        var afterFirst = handler.Requests;
        await media.EnrichFromOpenLibraryAsync(book.Id, "Throttled Book", null, null);
        await media.EnrichFromOpenLibraryAsync(book.Id, "Throttled Book", null, null);

        Assert.True(afterFirst > 0, "the first attempt should have queried Open Library");
        Assert.Equal(afterFirst, handler.Requests);
    }

    [Fact]
    public async Task EnrichFromOpenLibrary_ThrottlesPerBook()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var handler = new CountingHandler();
        var config = new ConfigurationBuilder().Build();
        var media = new MediaService(
            db,
            new TmdbService(new HttpClient(new CountingHandler()), NullLogger<TmdbService>.Instance, config),
            new OpenLibraryService(new HttpClient(handler), NullLogger<OpenLibraryService>.Instance, config),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<MediaService>.Instance);

        var first = await media.CreateAsync("Throttled Book A", MediaType.Book, length: 200);
        var second = await media.CreateAsync("Throttled Book B", MediaType.Book, length: 200);

        await media.EnrichFromOpenLibraryAsync(first.Id, "Throttled Book A", null, null);
        var afterFirst = handler.Requests;
        await media.EnrichFromOpenLibraryAsync(second.Id, "Throttled Book B", null, null);

        Assert.True(handler.Requests > afterFirst, "a different book is not held back by the first one");
    }
}
