using TaleTrackApp.Data;
using TaleTrackApp.Model;
using Microsoft.EntityFrameworkCore;

namespace TaleTrackApp.Features.TrackingEvent;

public class TrackingEventService
{
    private readonly AppDbContext _context;
    private readonly ILogger<TrackingEventService> _logger;

    public TrackingEventService(AppDbContext context, ILogger<TrackingEventService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Model.TrackingEvent> CreateAsync(int userId, int mediaId, int? progress = null)
    {
        var trackingEvent = new Model.TrackingEvent
        {
            UserId = userId,
            MediaId = mediaId,
            Progress = progress,
            EventDate = DateTime.UtcNow
        };

        _context.TrackingEvents.Add(trackingEvent);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"TrackingEvent created for User {userId}, Media {mediaId}");
        return trackingEvent;
    }

    public async Task<List<Model.TrackingEvent>> GetByUserIdAsync(int userId)
    {
        return await _context.TrackingEvents
            .Where(te => te.UserId == userId)
            .Include(te => te.Media)
            .ToListAsync();
    }

    public async Task<List<Model.TrackingEvent>> GetByUserIdWithFiltersAsync(
        int userId,
        string? mediaType = null,
        int? limit = null,
        string? orderBy = null)
    {
        var query = _context.TrackingEvents
            .Where(te => te.UserId == userId)
            .Include(te => te.Media)
            .AsQueryable();

        // Filtrar por tipo de media si se especifica
        if (!string.IsNullOrEmpty(mediaType))
        {
            query = query.Where(te => te.Media!.Type == mediaType);
        }

        // Aplicar ordenamiento
        if (!string.IsNullOrEmpty(orderBy))
        {
            query = orderBy.ToLower() switch
            {
                "title_asc" => query.OrderBy(te => te.Media!.Title),
                "title_desc" => query.OrderByDescending(te => te.Media!.Title),
                "date_asc" => query.OrderBy(te => te.EventDate),
                "date_desc" => query.OrderByDescending(te => te.EventDate),
                _ => query.OrderByDescending(te => te.EventDate) // default: más recientes primero
            };
        }
        else
        {
            query = query.OrderByDescending(te => te.EventDate);
        }

        // Aplicar límite si se especifica
        if (limit.HasValue && limit > 0)
        {
            query = query.Take(limit.Value);
        }

        return await query.ToListAsync();
    }
}
