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
    /// One tracking event per (user, media) — a matching row is updated in place; otherwise a
    /// new row is inserted. Season/episode are null for movies and books.
    /// </summary>
    public async Task<Model.TrackingEvent> UpsertAsync(
        Guid userId, Guid mediaId, int? progress,
        int? season = null, int? episode = null)
    {
        var existing = await _context.TrackingEvents
            .FirstOrDefaultAsync(te => te.UserId == userId && te.MediaId == mediaId);

        if (existing != null)
        {
            ApplyProgress(existing, progress, season, episode);
            existing.EventDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("TrackingEvent updated for User {UserId}, Media {MediaId}", userId, mediaId);
            return existing;
        }

        var trackingEvent = new Model.TrackingEvent
        {
            UserId = userId,
            MediaId = mediaId,
            Progress = progress,
            Season = season,
            Episode = episode,
            EventDate = DateTime.UtcNow
        };

        _context.TrackingEvents.Add(trackingEvent);
        await _context.SaveChangesAsync();

        _logger.LogInformation("TrackingEvent created for User {UserId}, Media {MediaId}", userId, mediaId);
        return trackingEvent;
    }

    /// <summary>
    /// Applies a new report to an existing row in place. For a series, only an equal or later
    /// (season, episode) than what's already stored is applied — rewatching an earlier episode
    /// is ignored, so the furthest reached never moves backward. Progress itself only ever rises,
    /// whether within the same episode or for a movie/book (which has none).
    /// </summary>
    private static void ApplyProgress(Model.TrackingEvent existing, int? progress, int? season, int? episode)
    {
        if (season.HasValue && episode.HasValue)
        {
            var isFurther = existing.Season is not int es || existing.Episode is not int ee
                || season > es || (season == es && episode > ee);

            if (isFurther)
            {
                existing.Season = season;
                existing.Episode = episode;
                existing.Progress = progress;
                return;
            }

            if (season != existing.Season || episode != existing.Episode)
                return; // an earlier episode being rewatched — leave the furthest reached alone
        }

        if (progress.HasValue)
            existing.Progress = Math.Max(existing.Progress ?? 0, progress.Value);
    }

    public async Task<Model.TrackingEvent?> GetForMediaAsync(Guid userId, Guid mediaId)
    {
        return await _context.TrackingEvents
            .FirstOrDefaultAsync(te => te.UserId == userId && te.MediaId == mediaId);
    }

    /// <summary>Removes the tracking event for (user, media), if any — takes the media out of their library.</summary>
    public async Task<bool> DeleteAllForMediaAsync(Guid userId, Guid mediaId)
    {
        var existing = await _context.TrackingEvents
            .FirstOrDefaultAsync(te => te.UserId == userId && te.MediaId == mediaId);

        if (existing == null) return false;

        _context.TrackingEvents.Remove(existing);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted tracking event for User {UserId}, Media {MediaId}", userId, mediaId);
        return true;
    }

    /// <summary>Overrides the progress of the user's tracking event for a media — unlike <see cref="UpsertAsync"/>, this can lower it.</summary>
    public async Task<Model.TrackingEvent?> SetProgressAsync(Guid userId, Guid mediaId, int progress)
    {
        var existing = await GetForMediaAsync(userId, mediaId);
        if (existing == null) return null;

        existing.Progress = progress;
        existing.EventDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Progress set to {Progress} for User {UserId}, Media {MediaId}",
            progress, userId, mediaId);
        return existing;
    }
}
