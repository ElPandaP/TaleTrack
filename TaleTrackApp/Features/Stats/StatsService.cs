using TaleTrackApp.Data;
using Microsoft.EntityFrameworkCore;

namespace TaleTrackApp.Features.Stats;

/// <summary>Per-type consumption counts for a year.</summary>
public record TypeBreakdown(int Book, int Movie, int Series);

/// <summary>Yearly consumption summary for a single user.</summary>
public record YearlyStats(
    int Year,
    int Total,
    TypeBreakdown ByType,
    int[] ByMonth,
    int ReviewCount);

public class StatsService
{
    private readonly AppDbContext _context;
    private readonly ILogger<StatsService> _logger;

    public StatsService(AppDbContext context, ILogger<StatsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Counts distinct media the user has a tracking event for during <paramref name="year"/>.
    /// Each media is bucketed into the month of its most recent event that year.
    /// </summary>
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
            Book: latestPerMedia.Count(te => te.Media!.Type == "Book"),
            Movie: latestPerMedia.Count(te => te.Media!.Type == "Movie"),
            Series: latestPerMedia.Count(te => te.Media!.Type == "Series"));

        var reviewCount = await _context.Reviews.CountAsync(r => r.UserId == userId);

        _logger.LogInformation("Yearly stats for user {UserId} ({Year}): {Total} titles", userId, year, latestPerMedia.Count);
        return new YearlyStats(year, latestPerMedia.Count, byType, byMonth, reviewCount);
    }
}
