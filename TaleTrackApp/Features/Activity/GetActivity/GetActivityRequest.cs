using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Activity.GetActivity;

public class GetActivityRequest
{
    [RegularExpression(@"^(mine|friends|all)?$", ErrorMessage = "scope must be 'mine', 'friends', 'all' or empty")]
    public string? Scope { get; set; }

    [Range(1, 500)]
    public int? Limit { get; set; }

    /// <summary>When set, returns just that user's activity (public profile).</summary>
    public Guid? UserId { get; set; }
}
