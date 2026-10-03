using TaleTrackApp.Data;
using Microsoft.EntityFrameworkCore;

namespace TaleTrackApp.Features.Stats;

/// <summary>Per-type consumption counts for a year.</summary>
/// <param name="Book">Books with activity in the year.</param>
/// <param name="Movie">Films with activity in the year.</param>
/// <param name="Series">Series with activity in the year.</param>
public record TypeBreakdown(int Book, int Movie, int Series);

/// <summary>Yearly consumption summary for a single user.</summary>
/// <param name="Year">The year summarized.</param>
/// <param name="Total">Distinct media with activity in the year.</param>
/// <param name="ByType">The total split by media type.</param>
/// <param name="ByMonth">Twelve counts, January first. Each media counts in the month of its latest activity that year.</param>
/// <param name="ReviewCount">Reviews the user has written in total, not only in this year.</param>
public record YearlyStats(
    int Year,
    int Total,
    TypeBreakdown ByType,
    int[] ByMonth,
    int ReviewCount);

/// <summary>Computes the yearly consumption summary shown on the home page.</summary>
public class StatsService
{
    /// <summary>Database context used to read tracking and reviews.</summary>
    private readonly AppDbContext _context;
    /// <summary>Logger for this service.</summary>
    private readonly ILogger<StatsService> _logger;

    /// <summary>Creates the service with its database context and logger.</summary>
    public StatsService(AppDbContext context, ILogger<StatsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Summarizes <paramref name="year"/> for a user: the media whose tracking was last updated in
    /// that year, split by type and by the month of that update, plus the user's total review count.
    /// </summary>
    /// <param name="userId">The user to summarize.</param>
    /// <param name="year">Calendar year, in UTC.</param>
    public async Task<YearlyStats> GetYearlyAsync(Guid userId, int year)
    {
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1);

        var events = await _context.TrackingEvents
            .Where(te => te.UserId == userId && te.EventDate >= from && te.EventDate < to)
            .Include(te => te.Media)
            .ToListAsync();

        var latestPerMedia = events
            .Where(te => te.Media != null)
            .GroupBy(te => te.MediaId)
            .Select(g => g.OrderByDescending(x => x.EventDate).First())
            .ToList();

        var byMonth = new int[12];
        foreach (var te in latestPerMedia)
            byMonth[te.EventDate.Month - 1]++;

        var byType = new TypeBreakdown(
            Book: latestPerMedia.Count(te => te.Media!.Type == Model.MediaType.Book),
            Movie: latestPerMedia.Count(te => te.Media!.Type == Model.MediaType.Movie),
            Series: latestPerMedia.Count(te => te.Media!.Type == Model.MediaType.Series));

        var reviewCount = await _context.Reviews.CountAsync(r => r.UserId == userId);

        _logger.LogInformation("Yearly stats for user {UserId} ({Year}): {Total} titles", userId, year, latestPerMedia.Count);
        return new YearlyStats(year, latestPerMedia.Count, byType, byMonth, reviewCount);
    }
}
