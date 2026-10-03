namespace TaleTrackApp.Features.TrackingEvent.EditTrackingProgress;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Body of <c>PUT /api/tracking/{mediaId}</c>. Send either <see cref="Progress"/> or both
/// <see cref="Season"/> and <see cref="Episode"/>.
/// </summary>
public class EditTrackingProgressRequest
{
    /// <summary>New percentage, 0-100.</summary>
    [Range(0, 100, ErrorMessage = "Progress must be between 0 and 100")]
    public int? Progress { get; set; }

    /// <summary>Series only: season of the episode reached, in place of a raw percentage.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Season must be 1 or greater")]
    public int? Season { get; set; }

    /// <summary>Series only: episode of `season` reached.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Episode must be 1 or greater")]
    public int? Episode { get; set; }
}
