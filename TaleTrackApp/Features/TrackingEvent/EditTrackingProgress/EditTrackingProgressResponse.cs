namespace TaleTrackApp.Features.TrackingEvent.EditTrackingProgress;

public class EditTrackingProgressResponse
{
    /// <summary>Always <c>true</c>.</summary>
    public bool Success { get; set; }

    /// <summary>Human-readable confirmation in English.</summary>
    public required string Message { get; set; }

    /// <summary>The stored progress after the update.</summary>
    public required EditedProgressData Data { get; set; }
}

public class EditedProgressData
{
    /// <summary>Stored percentage, 0-100. Null when the tracking has none (e.g. a series marked by episode only).</summary>
    public int? Progress { get; set; }
}
