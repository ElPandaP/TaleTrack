namespace TaleTrackApp.Features.TrackingEvent.EditTrackingProgress;

using System.ComponentModel.DataAnnotations;

public class EditTrackingProgressRequest
{
    [Range(0, 100, ErrorMessage = "Progress debe estar entre 0 y 100")]
    public int Progress { get; set; }
}
