using TaleTrackApp.Data;
using TaleTrackApp.Model;
using Microsoft.EntityFrameworkCore;

namespace TaleTrackApp.Features.TrackingEvent;

/// <summary>
/// Stores what each user is consuming. There is a single tracking row per user and media, which
/// every new report overwrites.
/// </summary>
public class TrackingEventService
{
    /// <summary>Database context used to read and write tracking rows.</summary>
    private readonly AppDbContext _context;
    /// <summary>Logger for this service.</summary>
    private readonly ILogger<TrackingEventService> _logger;

    /// <summary>Creates the service with its database context and logger.</summary>
    public TrackingEventService(AppDbContext context, ILogger<TrackingEventService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Records a progress report: updates the user's tracking row for the media, or creates it if
    /// there is none. Season and episode are only given for series.
    /// </summary>
    /// <param name="userId">The user reporting progress.</param>
    /// <param name="mediaId">The media being tracked.</param>
    /// <param name="progress">Percentage 0-100 (for a series, through the current episode).</param>
    /// <param name="season">Season of the episode, for a series.</param>
    /// <param name="episode">Episode within the season, for a series.</param>
    /// <returns>The stored tracking row.</returns>
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
    /// Applies a new report to an existing row. The latest report always wins, so progress can go
    /// down as well as up. For a series, a report with a season and episode replaces the stored
    /// episode and its progress.
    /// </summary>
    private static void ApplyProgress(Model.TrackingEvent existing, int? progress, int? season, int? episode)
    {
        if (season.HasValue && episode.HasValue)
        {
            existing.Season = season;
            existing.Episode = episode;
            existing.Progress = progress;
            return;
        }

        if (progress.HasValue)
            existing.Progress = progress.Value;
    }

    /// <summary>The user's tracking row for a media, or null if they do not track it.</summary>
    public async Task<Model.TrackingEvent?> GetForMediaAsync(Guid userId, Guid mediaId)
    {
        return await _context.TrackingEvents
            .FirstOrDefaultAsync(te => te.UserId == userId && te.MediaId == mediaId);
    }

    /// <summary>Removes the user's tracking of a media, which takes it out of their library.</summary>
    /// <returns>False if the user was not tracking it.</returns>
    public async Task<bool> DeleteForMediaAsync(Guid userId, Guid mediaId)
    {
        var existing = await _context.TrackingEvents
            .FirstOrDefaultAsync(te => te.UserId == userId && te.MediaId == mediaId);

        if (existing == null) return false;

        _context.TrackingEvents.Remove(existing);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted tracking event for User {UserId}, Media {MediaId}", userId, mediaId);
        return true;
    }

    /// <summary>
    /// Sets the progress of the user's existing tracking row for a media. Unlike
    /// <see cref="UpsertAsync"/>, it never creates one and returns null when there is none.
    /// </summary>
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

    /// <summary>
    /// Manually corrects the user's progress on a media. With a season and episode, that episode is
    /// stored as fully watched; otherwise the percentage is set outright. It never creates a
    /// tracking row.
    /// </summary>
    /// <returns>
    /// The updated row, or null when the user has no tracking for the media or nothing was given.
    /// </returns>
    public async Task<Model.TrackingEvent?> EditProgressAsync(
        Guid userId, Guid mediaId, int? progress, int? season, int? episode)
    {
        // Earlier episodes count as watched too: the overall percentage is derived from the stored
        // episode when it is read (see SeriesProgressCalculator).
        if (season.HasValue && episode.HasValue)
        {
            if (await GetForMediaAsync(userId, mediaId) == null) return null;
            return await UpsertAsync(userId, mediaId, progress: 100, season, episode);
        }

        return progress.HasValue
            ? await SetProgressAsync(userId, mediaId, progress.Value)
            : null;
    }
}
