using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.RequestDeletion;

public class RequestAccountDeletionRequest
{
    /// <summary>UI locale of the requester ("es"/"en").</summary>
    [StringLength(10)]
    public string? Locale { get; set; }
}
