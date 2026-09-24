using TaleTrackApp.Data;
using TaleTrackApp.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace TaleTrackApp.Features.Media;

public class MediaService
{
    private readonly AppDbContext _context;
    private readonly TmdbService _tmdb;
    private readonly OpenLibraryService _openLibrary;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MediaService> _logger;

    /// <summary>How long after an Open Library lookup the same book is not looked up again.</summary>
    private static readonly TimeSpan OpenLibraryRetryDelay = TimeSpan.FromHours(12);

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

    public async Task<Model.Media?> GetByIdAsync(Guid id)
    {
        return await _context.Medias.FindAsync(id);
    }

    /// <summary>Whether a movie/series still lacks what TMDB can fill in (poster, synopsis or either title). TMDB needs a language to search.</summary>
    public static bool NeedsTmdbEnrichment(Model.Media media, string? language) =>
        !string.IsNullOrWhiteSpace(language) &&
        (string.IsNullOrWhiteSpace(media.PosterUrl) || string.IsNullOrWhiteSpace(media.Description) ||
         string.IsNullOrWhiteSpace(media.TitleEN) || string.IsNullOrWhiteSpace(media.TitleES));

    /// <summary>Whether a book still lacks what OpenLibrary can fill in (cover, author or synopsis).</summary>
    public static bool NeedsOpenLibraryEnrichment(Model.Media media) =>
        string.IsNullOrWhiteSpace(media.PosterUrl) || string.IsNullOrWhiteSpace(media.Author) ||
        string.IsNullOrWhiteSpace(media.Description);

    private const int MaxDescriptionLength = 1000; // matches Media.Description's StringLength

    /// <summary>Trims a synopsis to what Media.Description can hold, ending in an ellipsis when it is cut.</summary>
    private static string ClipDescription(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length <= MaxDescriptionLength) return trimmed;

        var cut = MaxDescriptionLength - 1;
        if (char.IsHighSurrogate(trimmed[cut - 1])) cut--; // don't split an emoji in half
        return trimmed[..cut].TrimEnd() + "…";
    }

    public async Task<Model.Media> CreateAsync(string title, MediaType type, int length,
        string? author = null, string? isbn = null, string? language = null)
    {
        var media = new Model.Media
        {
            // Spanish only when explicitly detected; everything else (English,
            // unsupported/unknown languages, books with no language concept) defaults
            // to the English field.
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

        // 3. Deduplicate by title alone (either language) — catches the same movie/series
        // tracked again after the Netflix UI language changed (e.g. "Rick y Morty" vs
        // "Rick and Morty"), once TMDB enrichment has recorded the other-language title.
        var byTitle = await _context.Medias
            .FirstOrDefaultAsync(m => (m.TitleEN == title || m.TitleES == title) && m.Type == type);
        if (byTitle != null)
        {
            _logger.LogInformation("Media found by title: {Title} (ID: {Id})", byTitle.TitleEN ?? byTitle.TitleES, byTitle.Id);
            return byTitle;
        }

        return await CreateAsync(title, type, length, author, isbn, language);
    }

    public async Task ApplyEnrichmentAsync(Guid mediaId, OpenLibraryResult result)
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

    public async Task ApplyTmdbEnrichmentAsync(Guid mediaId, TmdbResult result)
    {
        var media = await _context.Medias.FindAsync(mediaId);
        if (media == null) return;

        if (!string.IsNullOrWhiteSpace(result.PosterUrl) && string.IsNullOrWhiteSpace(media.PosterUrl))
            media.PosterUrl = result.PosterUrl;
        if (!string.IsNullOrWhiteSpace(result.Description) && string.IsNullOrWhiteSpace(media.Description))
            media.Description = ClipDescription(result.Description);
        // Length is a movie-only concept here — a series' episodes vary in length, so
        // there's no single "length" worth recording for one (see SeasonEpisodeCounts).
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
    /// Looks the book up on OpenLibrary and fills in whatever it is missing. Does nothing if there is no match.
    /// Every sync of a book that is still incomplete asks for this, and Open Library asks clients to go
    /// easy on it, so a book is looked up at most once per <see cref="OpenLibraryRetryDelay"/>.
    /// </summary>
    public async Task EnrichFromOpenLibraryAsync(Guid mediaId, string title, string? author, string? isbn)
    {
        var attemptKey = $"openlibrary-lookup:{mediaId}";
        if (_cache.TryGetValue(attemptKey, out _)) return;
        _cache.Set(attemptKey, true, OpenLibraryRetryDelay);

        var result = await _openLibrary.EnrichAsync(title, author, isbn);
        if (result != null)
            await ApplyEnrichmentAsync(mediaId, result);
    }
}
