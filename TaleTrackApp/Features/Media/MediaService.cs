using TaleTrackApp.Data;
using TaleTrackApp.Model;
using Microsoft.EntityFrameworkCore;

namespace TaleTrackApp.Features.Media;

public class MediaService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MediaService> _logger;

    public MediaService(AppDbContext context, ILogger<MediaService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Model.Media?> GetByIdAsync(int id)
    {
        return await _context.Medias.FindAsync(id);
    }

    public async Task<Model.Media> CreateAsync(string title, string type, int length,
        string? author = null, string? isbn = null)
    {
        var media = new Model.Media
        {
            Title = title,
            Type = type,
            Length = length,
            Author = author,
            Isbn = isbn,
            FirstTrackedAt = DateTime.UtcNow
        };

        _context.Medias.Add(media);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Media created: {Title}", media.Title);
        return media;
    }

    public async Task<Model.Media> FindOrCreateAsync(string title, string type, int length,
        string? author = null, string? isbn = null)
    {
        // 1. Deduplicate by ISBN (most precise)
        if (!string.IsNullOrWhiteSpace(isbn))
        {
            var byIsbn = await _context.Medias
                .FirstOrDefaultAsync(m => m.Isbn == isbn && m.Type == type);
            if (byIsbn != null)
            {
                _logger.LogInformation("Media found by ISBN: {Title} (ID: {Id})", byIsbn.Title, byIsbn.Id);
                return byIsbn;
            }
        }

        // 2. Deduplicate by title + author
        if (!string.IsNullOrWhiteSpace(author))
        {
            var byTitleAuthor = await _context.Medias
                .FirstOrDefaultAsync(m => m.Title == title && m.Author == author && m.Type == type);
            if (byTitleAuthor != null)
            {
                _logger.LogInformation("Media found by title+author: {Title} (ID: {Id})", byTitleAuthor.Title, byTitleAuthor.Id);
                return byTitleAuthor;
            }
        }

        // 3. Deduplicate by title alone (existing behaviour)
        var byTitle = await _context.Medias
            .FirstOrDefaultAsync(m => m.Title == title && m.Type == type);
        if (byTitle != null)
        {
            _logger.LogInformation("Media found by title: {Title} (ID: {Id})", byTitle.Title, byTitle.Id);
            return byTitle;
        }

        // 4. Deduplicate by AltTitle — catches the same movie/series tracked again
        // after the Netflix UI language changed (e.g. "Rick y Morty" vs "Rick and
        // Morty"), once TMDB enrichment has recorded the other-language title.
        var byAltTitle = await _context.Medias
            .FirstOrDefaultAsync(m => m.AltTitle == title && m.Type == type);
        if (byAltTitle != null)
        {
            _logger.LogInformation("Media found by AltTitle: {Title} (ID: {Id})", byAltTitle.Title, byAltTitle.Id);
            return byAltTitle;
        }

        return await CreateAsync(title, type, length, author, isbn);
    }

    public async Task ApplyEnrichmentAsync(int mediaId, OpenLibraryResult result)
    {
        var media = await _context.Medias.FindAsync(mediaId);
        if (media == null) return;

        if (!string.IsNullOrWhiteSpace(result.Author) && string.IsNullOrWhiteSpace(media.Author))
            media.Author = result.Author;
        if (!string.IsNullOrWhiteSpace(result.CoverUrl) && string.IsNullOrWhiteSpace(media.PosterUrl))
            media.PosterUrl = result.CoverUrl;
        if (!string.IsNullOrWhiteSpace(result.Isbn) && string.IsNullOrWhiteSpace(media.Isbn))
            media.Isbn = result.Isbn;

        media.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Media {MediaId} enriched from OpenLibrary", mediaId);
    }

    public async Task ApplyTmdbEnrichmentAsync(int mediaId, TmdbResult result)
    {
        var media = await _context.Medias.FindAsync(mediaId);
        if (media == null) return;

        if (!string.IsNullOrWhiteSpace(result.PosterUrl) && string.IsNullOrWhiteSpace(media.PosterUrl))
            media.PosterUrl = result.PosterUrl;
        if (result.RuntimeMinutes is int minutes && minutes > 0 && media.Length <= 0)
            media.Length = minutes;
        if (!string.IsNullOrWhiteSpace(result.AltTitle) && string.IsNullOrWhiteSpace(media.AltTitle))
            media.AltTitle = result.AltTitle;

        media.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Media {MediaId} enriched from TMDB", mediaId);
    }
}
