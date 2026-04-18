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

    public async Task<List<Model.Media>> GetAllAsync()
    {
        return await _context.Medias.ToListAsync();
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

    public async Task<List<Model.Media>> GetWithFiltersAsync(
        string? mediaType = null,
        int? limit = null,
        string? orderBy = null)
    {
        var query = _context.Medias.AsQueryable();

        // Filtrar por tipo de media si se especifica
        if (!string.IsNullOrEmpty(mediaType))
        {
            query = query.Where(m => m.Type == mediaType);
        }

        // Aplicar ordenamiento
        if (!string.IsNullOrEmpty(orderBy))
        {
            query = orderBy.ToLower() switch
            {
                "title_asc" => query.OrderBy(m => m.Title),
                "title_desc" => query.OrderByDescending(m => m.Title),
                "date_asc" => query.OrderBy(m => m.FirstTrackedAt),
                "date_desc" => query.OrderByDescending(m => m.FirstTrackedAt),
                _ => query.OrderByDescending(m => m.FirstTrackedAt) // default: más recientes primero
            };
        }
        else
        {
            query = query.OrderByDescending(m => m.FirstTrackedAt);
        }

        // Aplicar límite si se especifica
        if (limit.HasValue && limit > 0)
        {
            query = query.Take(limit.Value);
        }

        return await query.ToListAsync();
    }
}
