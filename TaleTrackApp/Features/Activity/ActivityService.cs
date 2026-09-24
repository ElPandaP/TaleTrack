using TaleTrackApp.Data;
using TaleTrackApp.Model;
using Microsoft.EntityFrameworkCore;

namespace TaleTrackApp.Features.Activity;

/// <summary>One entry of the activity feed: someone started, finished or reviewed a media.</summary>
/// <param name="Id">Stable id of the entry, unique within a feed.</param>
/// <param name="UserId">Who did it.</param>
/// <param name="Username">Public username of that user.</param>
/// <param name="AvatarUrl">Profile photo URL of that user. Null when they have none.</param>
/// <param name="Kind">`started`, `finished` (progress reached 100%) or `reviewed`.</param>
/// <param name="Date">When it happened: the latest progress report for started and finished, the review date for reviewed.</param>
/// <param name="MediaId">The media involved.</param>
/// <param name="MediaTitleEN">English title of the media, if known.</param>
/// <param name="MediaTitleES">Spanish title of the media, if known.</param>
/// <param name="MediaType">`Movie`, `Series` or `Book`.</param>
/// <param name="MediaPosterUrl">Poster or cover image URL of the media.</param>
/// <param name="Rating">Rating 1-10. Only for `reviewed`.</param>
/// <param name="Comment">Review comment. Only for `reviewed`.</param>
public record ActivityItem(
    string Id,
    Guid UserId,
    string Username,
    string? AvatarUrl,
    string Kind,
    DateTime Date,
    Guid MediaId,
    string? MediaTitleEN,
    string? MediaTitleES,
    string MediaType,
    string? MediaPosterUrl,
    int? Rating,
    string? Comment);

public class ActivityService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ActivityService> _logger;

    public ActivityService(AppDbContext context, ILogger<ActivityService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private static bool ShowProgress(Model.User u, Model.MediaType type) => type switch
    {
        Model.MediaType.Book => u.ShareBookProgress,
        Model.MediaType.Movie => u.ShareMovieProgress,
        Model.MediaType.Series => u.ShareSeriesProgress,
        _ => true,
    };

    private static bool ShowReviews(Model.User u, Model.MediaType type) => type switch
    {
        Model.MediaType.Book => u.ShareBookReviews,
        Model.MediaType.Movie => u.ShareMovieReviews,
        Model.MediaType.Series => u.ShareSeriesReviews,
        _ => true,
    };

    /// <summary>
    /// Feed derived from tracking events + reviews. `scope`: "mine" | "friends" | "all".
    /// Another user's items are filtered by that user's per-type privacy; the viewer
    /// always sees all of their own.
    /// </summary>
    public async Task<List<ActivityItem>> GetFeedAsync(Guid viewerId, string scope, int limit)
    {
        var friendIds = await FriendIdsAsync(viewerId);
        var userIds = scope.ToLowerInvariant() switch
        {
            "mine" => [viewerId],
            "friends" => friendIds,
            _ => friendIds.Append(viewerId).ToList(),
        };
        return await BuildAsync(viewerId, userIds, limit);
    }

    /// <summary>One user's activity, for their public profile. Empty unless the
    /// viewer is that user or a friend.</summary>
    public async Task<List<ActivityItem>> GetForUserAsync(Guid viewerId, Guid targetUserId, int limit)
    {
        if (viewerId != targetUserId)
        {
            var friendIds = await FriendIdsAsync(viewerId);
            if (!friendIds.Contains(targetUserId)) return [];
        }
        return await BuildAsync(viewerId, [targetUserId], limit);
    }

    private async Task<List<Guid>> FriendIdsAsync(Guid userId) =>
        await _context.Friendships
            .Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

    private async Task<List<ActivityItem>> BuildAsync(Guid viewerId, List<Guid> userIds, int limit)
    {
        if (userIds.Count == 0) return [];

        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var events = await _context.TrackingEvents
            .Where(te => userIds.Contains(te.UserId))
            .Include(te => te.Media)
            .ToListAsync();

        var reviews = await _context.Reviews
            .Where(r => userIds.Contains(r.UserId))
            .Include(r => r.Media)
            .ToListAsync();

        var items = new List<ActivityItem>();

        // One row per (user, media) — it doubles as both "started" (however long ago it was
        // last touched) and, when it reached 100%, "finished" too.
        foreach (var e in events.Where(e => e.Media != null))
        {
            var media = e.Media!;
            var u = users[e.UserId];
            if (e.UserId != viewerId && !ShowProgress(u, media.Type)) continue;

            items.Add(new ActivityItem(
                $"start-{e.UserId}-{e.MediaId}", u.Id, u.Username, u.AvatarUrl,
                "started", e.EventDate, media.Id, media.TitleEN, media.TitleES, media.Type.ToString(), media.PosterUrl, null, null));

            if (e.Progress == 100)
            {
                items.Add(new ActivityItem(
                    $"finish-{e.UserId}-{e.MediaId}", u.Id, u.Username, u.AvatarUrl,
                    "finished", e.EventDate, media.Id, media.TitleEN, media.TitleES, media.Type.ToString(), media.PosterUrl, null, null));
            }
        }

        foreach (var r in reviews.Where(r => r.Media != null))
        {
            var u = users[r.UserId];
            if (r.UserId != viewerId && !ShowReviews(u, r.Media!.Type)) continue;
            items.Add(new ActivityItem(
                $"review-{r.Id}", u.Id, u.Username, u.AvatarUrl,
                "reviewed", r.CreatedAt, r.Media!.Id, r.Media.TitleEN, r.Media.TitleES, r.Media.Type.ToString(), r.Media.PosterUrl, r.Rating, r.Comment));
        }

        return items
            .OrderByDescending(i => i.Date)
            .Take(limit)
            .ToList();
    }
}
