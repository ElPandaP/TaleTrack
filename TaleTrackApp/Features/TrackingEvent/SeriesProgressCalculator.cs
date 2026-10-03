namespace TaleTrackApp.Features.TrackingEvent;

/// <summary>
/// Works out how much of a series a user has watched from the latest season and episode they
/// reported. It assumes every earlier episode was watched too and that all episodes weigh the same.
/// </summary>
public static class SeriesProgressCalculator
{
    /// <summary>
    /// The progress to show for a media given the user's tracking of it. For a series it is derived
    /// from the latest season and episode reported, falling back to the stored progress when it
    /// cannot be derived; for films and books it is the stored progress.
    /// </summary>
    /// <param name="media">The tracked media.</param>
    /// <param name="tracking">The user's tracking of it, or null when they do not track it.</param>
    /// <returns>A percentage from 0 to 100, or null when nothing is known.</returns>
    public static int? ProgressFor(Model.Media media, Model.TrackingEvent? tracking) =>
        media.Type == Model.MediaType.Series
            ? Calculate(media.SeasonEpisodeCounts, tracking?.Season, tracking?.Episode) ?? tracking?.Progress
            : tracking?.Progress;

    /// <summary>
    /// Percentage of a series watched up to and including <paramref name="episode"/> of
    /// <paramref name="season"/>.
    /// </summary>
    /// <param name="seasonEpisodeCounts">Number of episodes of each season, season 1 first.</param>
    /// <param name="season">Season of the latest episode reported, starting at 1.</param>
    /// <param name="episode">Episode number within that season.</param>
    /// <returns>A rounded percentage capped at 100, or null when the episode counts are unknown or the season is below 1.</returns>
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
