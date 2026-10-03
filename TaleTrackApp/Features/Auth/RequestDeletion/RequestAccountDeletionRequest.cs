using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.RequestDeletion;

/// <summary>Body of `POST /api/auth/request-account-deletion`.</summary>
public class RequestAccountDeletionRequest
{
    /// <summary>UI locale of the requester ("es"/"en"); the confirmation email is sent in it.</summary>
    [StringLength(10)]
    public string? Locale { get; set; }
}
