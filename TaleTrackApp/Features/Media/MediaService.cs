using TaleTrackApp.Data;
using TaleTrackApp.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace TaleTrackApp.Features.Media;

/// <summary>
/// Manages the shared media catalogue: finds or creates a media when someone tracks it, avoiding
/// duplicates, and fills in missing metadata from TMDB (films and series) or Open Library (books).
/// </summary>
public class MediaService
{
    /// <summary>Database context used to read and write media.</summary>
    private readonly AppDbContext _context;
    /// <summary>Client used to look films and series up on TMDB.</summary>
    private readonly TmdbService _tmdb;
    /// <summary>Client used to look books up on Open Library.</summary>
    private readonly OpenLibraryService _openLibrary;
    /// <summary>Remembers recent Open Library lookups so the same book is not looked up again too soon.</summary>
    private readonly IMemoryCache _cache;
    /// <summary>Logger for this service.</summary>
    private readonly ILogger<MediaService> _logger;

    /// <summary>How long after an Open Library lookup the same book is not looked up again.</summary>
    private static readonly TimeSpan OpenLibraryRetryDelay = TimeSpan.FromHours(12);

    /// <summary>Creates the service with its dependencies.</summary>
    public MediaService(
        AppDbContext context,
        TmdbService tmdb,
        OpenLibraryService openLibrary,
        IMemoryCache cache,
        ILogger<MediaService> logger)
    {
        _context = context;
        _tmdb = tmdb;
        _openLibrary = openLibrary;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>The media with this id, or null if it does not exist.</summary>
    public async Task<Model.Media?> GetByIdAsync(Guid id)
    {
        return await _context.Medias.FindAsync(id);
    }

    /// <summary>
    /// Whether a film or series still lacks something TMDB can fill in (poster, synopsis or either
    /// title). Always false without a language, because the TMDB search needs one.
    /// </summary>
    public static bool NeedsTmdbEnrichment(Model.Media media, string? language) =>
        !string.IsNullOrWhiteSpace(language) &&
        (string.IsNullOrWhiteSpace(media.PosterUrl) || string.IsNullOrWhiteSpace(media.Description) ||
         string.IsNullOrWhiteSpace(media.TitleEN) || string.IsNullOrWhiteSpace(media.TitleES));

    /// <summary>Whether a book still lacks something Open Library can fill in (cover, author or synopsis).</summary>
    public static bool NeedsOpenLibraryEnrichment(Model.Media media) =>
        string.IsNullOrWhiteSpace(media.PosterUrl) || string.IsNullOrWhiteSpace(media.Author) ||
        string.IsNullOrWhiteSpace(media.Description);

    /// <summary>Longest synopsis that fits in the database column.</summary>
    private const int MaxDescriptionLength = 1000; // matches Media.Description's StringLength

    /// <summary>Trims a synopsis to what <see cref="Model.Media.Description"/> can hold, ending in an ellipsis when it is cut.</summary>
    private static string ClipDescription(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length <= MaxDescriptionLength) return trimmed;

        var cut = MaxDescriptionLength - 1;
        if (char.IsHighSurrogate(trimmed[cut - 1])) cut--; // don't split an emoji in half
        return trimmed[..cut].TrimEnd() + "…";
    }

    /// <summary>
    /// Inserts a new media. The title goes to <c>TitleES</c> when <paramref name="language"/> is
    /// <c>es</c> and to <c>TitleEN</c> otherwise.
    /// </summary>
    public async Task<Model.Media> CreateAsync(string title, MediaType type, int length,
        string? author = null, string? isbn = null, string? language = null)
    {
        var media = new Model.Media
        {
            // Spanish only when explicitly detected; everything else (English, unknown
            // languages, books that carry no language) goes to the English field.
            TitleEN = language == "es" ? null : title,
            TitleES = language == "es" ? title : null,
            Type = type,
            Length = length,
            Author = author,
            Isbn = isbn,
            FirstTrackedAt = DateTime.UtcNow
        };

        _context.Medias.Add(media);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Media created: {Title}", media.TitleEN ?? media.TitleES);
        return media;
    }

    /// <summary>
    /// Returns the existing media that matches, or creates it. Matching is always within the same
    /// type and tries, in order: ISBN, title plus author, title alone. Titles are compared against
    /// both the English and the Spanish title.
    /// </summary>
    /// <param name="title">Title as reported by the client.</param>
    /// <param name="type">Media type.</param>
    /// <param name="length">Runtime in minutes or page count; only used when the media is created.</param>
    /// <param name="author">Book author, if known.</param>
    /// <param name="isbn">Book ISBN, if known.</param>
    /// <param name="language">Language of <paramref name="title"/> (<c>es</c> or <c>en</c>); only used when the media is created.</param>
    public async Task<Model.Media> FindOrCreateAsync(string title, MediaType type, int length,
        string? author = null, string? isbn = null, string? language = null)
    {
        // 1. Deduplicate by ISBN (most precise)
        if (!string.IsNullOrWhiteSpace(isbn))
        {
            var byIsbn = await _context.Medias
                .FirstOrDefaultAsync(m => m.Isbn == isbn && m.Type == type);
            if (byIsbn != null)
            {
                _logger.LogInformation("Media found by ISBN: {Title} (ID: {Id})", byIsbn.TitleEN ?? byIsbn.TitleES, byIsbn.Id);
                return byIsbn;
            }
        }

        // 2. Deduplicate by title (either language) + author
        if (!string.IsNullOrWhiteSpace(author))
        {
            var byTitleAuthor = await _context.Medias
                .FirstOrDefaultAsync(m => (m.TitleEN == title || m.TitleES == title) && m.Author == author && m.Type == type);
            if (byTitleAuthor != null)
            {
                _logger.LogInformation("Media found by title+author: {Title} (ID: {Id})", byTitleAuthor.TitleEN ?? byTitleAuthor.TitleES, byTitleAuthor.Id);
                return byTitleAuthor;
            }
        }

        // 3. Deduplicate by title alone (either language). Once TMDB has filled in the title in
        // the other language, this also matches a film or series reported after the Netflix UI
        // language changed (e.g. "Rick y Morty" vs "Rick and Morty").
        var byTitle = await _context.Medias
            .FirstOrDefaultAsync(m => (m.TitleEN == title || m.TitleES == title) && m.Type == type);
        if (byTitle != null)
        {
            _logger.LogInformation("Media found by title: {Title} (ID: {Id})", byTitle.TitleEN ?? byTitle.TitleES, byTitle.Id);
            return byTitle;
        }

        return await CreateAsync(title, type, length, author, isbn, language);
    }

    /// <summary>
    /// Copies Open Library data into a book, only into the fields that are still empty. Does nothing
    /// if the media no longer exists.
    /// </summary>
    public async Task ApplyOpenLibraryEnrichmentAsync(Guid mediaId, OpenLibraryResult result)
    {
        var media = await _context.Medias.FindAsync(mediaId);
        if (media == null) return;

        if (!string.IsNullOrWhiteSpace(result.Author) && string.IsNullOrWhiteSpace(media.Author))
            media.Author = result.Author;
        if (!string.IsNullOrWhiteSpace(result.CoverUrl) && string.IsNullOrWhiteSpace(media.PosterUrl))
            media.PosterUrl = result.CoverUrl;
        if (!string.IsNullOrWhiteSpace(result.Isbn) && string.IsNullOrWhiteSpace(media.Isbn))
            media.Isbn = result.Isbn;
        if (!string.IsNullOrWhiteSpace(result.Description) && string.IsNullOrWhiteSpace(media.Description))
            media.Description = ClipDescription(result.Description);

        media.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Media {MediaId} enriched from OpenLibrary", mediaId);
    }

    /// <summary>
    /// Copies TMDB data into a film or series, only into the fields that are still empty. Does
    /// nothing if the media no longer exists.
    /// </summary>
    public async Task ApplyTmdbEnrichmentAsync(Guid mediaId, TmdbResult result)
    {
        var media = await _context.Medias.FindAsync(mediaId);
        if (media == null) return;

        if (!string.IsNullOrWhiteSpace(result.PosterUrl) && string.IsNullOrWhiteSpace(media.PosterUrl))
            media.PosterUrl = result.PosterUrl;
        if (!string.IsNullOrWhiteSpace(result.Description) && string.IsNullOrWhiteSpace(media.Description))
            media.Description = ClipDescription(result.Description);
        // Only films get a length: a series' episodes vary, so its progress relies on
        // SeasonEpisodeCounts instead.
        if (media.Type == MediaType.Movie && result.RuntimeMinutes is int minutes && minutes > 0 && media.Length <= 0)
            media.Length = minutes;
        if (!string.IsNullOrWhiteSpace(result.TitleEN) && string.IsNullOrWhiteSpace(media.TitleEN))
            media.TitleEN = result.TitleEN;
        if (!string.IsNullOrWhiteSpace(result.TitleES) && string.IsNullOrWhiteSpace(media.TitleES))
            media.TitleES = result.TitleES;
        if (result.SeasonEpisodeCounts is { Length: > 0 } && media.SeasonEpisodeCounts == null)
            media.SeasonEpisodeCounts = result.SeasonEpisodeCounts;

        media.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Media {MediaId} enriched from TMDB", mediaId);
    }

    /// <summary>Looks the media up on TMDB and fills in whatever it is missing. Does nothing if TMDB has no match.</summary>
    public async Task EnrichFromTmdbAsync(Guid mediaId, string title, MediaType type, string? language)
    {
        var result = await _tmdb.EnrichAsync(title, type, language);
        if (result != null)
            await ApplyTmdbEnrichmentAsync(mediaId, result);
    }

    /// <summary>
    /// Looks the book up on Open Library and fills in whatever it is missing. Does nothing if there
    /// is no match.
    /// </summary>
    /// <remarks>
    /// Every sync of a book that is still incomplete asks for this, and Open Library asks clients to
    /// go easy on it, so a book is looked up at most once per <see cref="OpenLibraryRetryDelay"/>.
    /// </remarks>
    public async Task EnrichFromOpenLibraryAsync(Guid mediaId, string title, string? author, string? isbn)
    {
        var attemptKey = $"openlibrary-lookup:{mediaId}";
        if (_cache.TryGetValue(attemptKey, out _)) return;
        _cache.Set(attemptKey, true, OpenLibraryRetryDelay);

        var result = await _openLibrary.EnrichAsync(title, author, isbn);
        if (result != null)
            await ApplyOpenLibraryEnrichmentAsync(mediaId, result);
    }
}
