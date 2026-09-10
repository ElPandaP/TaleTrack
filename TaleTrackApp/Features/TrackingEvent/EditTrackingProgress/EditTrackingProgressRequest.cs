namespace TaleTrackApp.Features.TrackingEvent.EditTrackingProgress;

using System.ComponentModel.DataAnnotations;

public class EditTrackingProgressRequest
{
    [Range(0, 100, ErrorMessage = "Progress must be between 0 and 100")]
    public int Progress { get; set; }
}
