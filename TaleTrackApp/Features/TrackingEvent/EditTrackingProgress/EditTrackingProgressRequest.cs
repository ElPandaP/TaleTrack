namespace TaleTrackApp.Features.TrackingEvent.EditTrackingProgress;

using System.ComponentModel.DataAnnotations;

public class EditTrackingProgressRequest
{
    /// <summary>New percentage, 0-100. Can be lower than the current one.</summary>
    [Range(0, 100, ErrorMessage = "Progress must be between 0 and 100")]
    public int? Progress { get; set; }

    /// <summary>Series only — the furthest episode reached, in place of a raw percentage.</summary>
    [Range(0, int.MaxValue)]
    public int? Season { get; set; }

    /// <summary>Series only: episode of `season` reached.</summary>
    [Range(0, int.MaxValue)]
    public int? Episode { get; set; }
}
