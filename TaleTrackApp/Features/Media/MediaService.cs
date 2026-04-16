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

    public async Task<Model.Media> CreateAsync(string title, string type, int length)
    {
        var media = new Model.Media
        {
            Title = title,
            Type = type,
            Length = length,
            FirstTrackedAt = DateTime.UtcNow
        };

        _context.Medias.Add(media);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Media created: {media.Title}");
        return media;
    }

    public async Task<Model.Media> FindOrCreateAsync(string title, string type, int length)
    {
        // Buscar media existente por título y tipo
        var existingMedia = await _context.Medias
            .FirstOrDefaultAsync(m => m.Title == title && m.Type == type);

        if (existingMedia != null)
        {
            _logger.LogInformation($"Media found: {existingMedia.Title} (ID: {existingMedia.Id})");
            return existingMedia;
        }

        // Si no existe, crear uno nuevo
        return await CreateAsync(title, type, length);
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
