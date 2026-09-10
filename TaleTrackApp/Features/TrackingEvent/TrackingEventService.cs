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

    /// <summary>
    /// One tracking event per (user, media, season, episode) — the most recent state wins.
    /// A matching row is updated in place (progress only ever rises); otherwise a new row is
    /// inserted. Season/episode are null for movies and books.
    /// </summary>
    public async Task<Model.TrackingEvent> UpsertAsync(
        int userId, int mediaId, int? progress,
        int? season = null, int? episode = null, string? episodeTitle = null)
    {
        var existing = await _context.TrackingEvents
            .Where(te => te.UserId == userId && te.MediaId == mediaId
                      && te.Season == season && te.Episode == episode)
            .OrderByDescending(te => te.EventDate)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            if (progress.HasValue)
                existing.Progress = Math.Max(existing.Progress ?? 0, progress.Value);
            if (!string.IsNullOrWhiteSpace(episodeTitle))
                existing.EpisodeTitle = episodeTitle;
            existing.EventDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("TrackingEvent updated for User {UserId}, Media {MediaId} (S{Season}E{Episode})",
                userId, mediaId, season, episode);
            return existing;
        }

        var trackingEvent = new Model.TrackingEvent
        {
            UserId = userId,
            MediaId = mediaId,
            Progress = progress,
            Season = season,
            Episode = episode,
            EpisodeTitle = episodeTitle,
            EventDate = DateTime.UtcNow
        };

        _context.TrackingEvents.Add(trackingEvent);
        await _context.SaveChangesAsync();

        _logger.LogInformation("TrackingEvent created for User {UserId}, Media {MediaId} (S{Season}E{Episode})",
            userId, mediaId, season, episode);
        return trackingEvent;
    }

    public async Task<Model.TrackingEvent?> GetLatestForMediaAsync(int userId, int mediaId)
    {
        return await _context.TrackingEvents
            .Where(te => te.UserId == userId && te.MediaId == mediaId)
            .OrderByDescending(te => te.EventDate)
            .FirstOrDefaultAsync();
    }

    /// <summary>Removes every tracking event for (user, media) — takes the media out of their library.</summary>
    public async Task<bool> DeleteAllForMediaAsync(int userId, int mediaId)
    {
        var events = await _context.TrackingEvents
            .Where(te => te.UserId == userId && te.MediaId == mediaId)
            .ToListAsync();

        if (events.Count == 0) return false;

        _context.TrackingEvents.RemoveRange(events);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted {Count} tracking event(s) for User {UserId}, Media {MediaId}",
            events.Count, userId, mediaId);
        return true;
    }

    /// <summary>Overrides the progress of the user's most recent tracking event for a media.</summary>
    public async Task<Model.TrackingEvent?> SetProgressAsync(int userId, int mediaId, int progress)
    {
        var latest = await _context.TrackingEvents
            .Where(te => te.UserId == userId && te.MediaId == mediaId)
            .OrderByDescending(te => te.EventDate)
            .FirstOrDefaultAsync();

        if (latest == null) return null;

        latest.Progress = progress;
        latest.EventDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Progress set to {Progress} for User {UserId}, Media {MediaId}",
            progress, userId, mediaId);
        return latest;
    }
}
