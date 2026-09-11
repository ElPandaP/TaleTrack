namespace TaleTrackApp.Features.TrackingEvent;

/// <summary>
/// A series' overall % watched, from the furthest (season, episode) reached — assumes every
/// earlier episode was also watched and that every episode runs the same length.
/// </summary>
public static class SeriesProgress
{
    public static int? Calculate(int[]? seasonEpisodeCounts, int? season, int? episode)
    {
        if (seasonEpisodeCounts is not { Length: > 0 } || season is not int s || s < 1 || episode is not int e)
            return null;

        var total = seasonEpisodeCounts.Sum();
        if (total <= 0) return null;

        var watched = 0;
        for (var i = 0; i < s - 1 && i < seasonEpisodeCounts.Length; i++)
            watched += seasonEpisodeCounts[i];
        watched += Math.Max(0, e);

        return (int)Math.Round(Math.Min(watched, total) * 100.0 / total);
    }
}
