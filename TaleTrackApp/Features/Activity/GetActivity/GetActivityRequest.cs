using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Activity.GetActivity;

public class GetActivityRequest
{
    /// <summary>Whose activity to show: `mine`, `friends` or `all` (default: the caller and their friends). Ignored when `userId` is set.</summary>
    [RegularExpression(@"^(mine|friends|all)?$", ErrorMessage = "scope must be 'mine', 'friends', 'all' or empty")]
    public string? Scope { get; set; }

    /// <summary>Maximum entries to return, 1-500. Defaults to 200.</summary>
    [Range(1, 500)]
    public int? Limit { get; set; }

    /// <summary>When set, returns just that user's activity (public profile).</summary>
    public Guid? UserId { get; set; }
}
