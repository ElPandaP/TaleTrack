using TaleTrackApp.Data;
using Microsoft.EntityFrameworkCore;

namespace TaleTrackApp.Features.Activity;

public record ActivityItem(
    string Id,
    int UserId,
    string Username,
    string? AvatarUrl,
    string Kind,          // "started" | "finished" | "reviewed"
    DateTime Date,
    int MediaId,
    string MediaTitle,
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

    private static bool ShowProgress(Model.User u, string type) => type switch
    {
        "Book" => u.ShareBookProgress,
        "Movie" => u.ShareMovieProgress,
        "Series" => u.ShareSeriesProgress,
        _ => true,
    };

    private static bool ShowReviews(Model.User u, string type) => type switch
    {
        "Book" => u.ShareBookReviews,
        "Movie" => u.ShareMovieReviews,
        "Series" => u.ShareSeriesReviews,
        _ => true,
    };

    /// <summary>
    /// Feed derived from tracking events + reviews. `scope`: "mine" | "friends" | "all".
    /// Another user's items are filtered by that user's per-type privacy; the viewer
    /// always sees all of their own.
    /// </summary>
    public async Task<List<ActivityItem>> GetFeedAsync(int viewerId, string scope, int limit)
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
    public async Task<List<ActivityItem>> GetForUserAsync(int viewerId, int targetUserId, int limit)
    {
        if (viewerId != targetUserId)
        {
            var friendIds = await FriendIdsAsync(viewerId);
            if (!friendIds.Contains(targetUserId)) return [];
        }
        return await BuildAsync(viewerId, [targetUserId], limit);
    }

    private async Task<List<int>> FriendIdsAsync(int userId) =>
        await _context.Friendships
            .Where(f => f.Status == "Accepted" && (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

    private async Task<List<ActivityItem>> BuildAsync(int viewerId, List<int> userIds, int limit)
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

        foreach (var g in events.Where(e => e.Media != null).GroupBy(e => new { e.UserId, e.MediaId }))
        {
            var media = g.First().Media!;
            var u = users[g.Key.UserId];
            if (g.Key.UserId != viewerId && !ShowProgress(u, media.Type)) continue;

            var started = g.OrderBy(x => x.EventDate).First();
            items.Add(new ActivityItem(
                $"start-{g.Key.UserId}-{g.Key.MediaId}", u.Id, u.Username, u.AvatarUrl,
                "started", started.EventDate, media.Id, media.Title, media.Type, media.PosterUrl, null, null));

            // With upsert, a title finished in one sitting is a single row — so a lone
            // Progress==100 event still counts as "finished".
            var finished = g.Where(x => x.Progress == 100).OrderByDescending(x => x.EventDate).FirstOrDefault();
            if (finished != null && (g.Count() == 1 || finished.EventDate > started.EventDate))
            {
                items.Add(new ActivityItem(
                    $"finish-{g.Key.UserId}-{g.Key.MediaId}", u.Id, u.Username, u.AvatarUrl,
                    "finished", finished.EventDate, media.Id, media.Title, media.Type, media.PosterUrl, null, null));
            }
        }

        foreach (var r in reviews.Where(r => r.Media != null))
        {
            var u = users[r.UserId];
            if (r.UserId != viewerId && !ShowReviews(u, r.Media!.Type)) continue;
            items.Add(new ActivityItem(
                $"review-{r.Id}", u.Id, u.Username, u.AvatarUrl,
                "reviewed", r.CreatedAt, r.Media!.Id, r.Media.Title, r.Media.Type, r.Media.PosterUrl, r.Rating, r.Comment));
        }

        return items
            .OrderByDescending(i => i.Date)
            .Take(limit)
            .ToList();
    }
}
