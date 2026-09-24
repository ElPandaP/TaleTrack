namespace TaleTrackApp.Features.User.EditUser;

using System.ComponentModel.DataAnnotations;

public class FeedPrivacyRequest
{
    /// <summary>Whether friends see the caller's book progress in the activity feed. Omit to leave unchanged.</summary>
    public bool? BookProgress { get; set; }
    /// <summary>Whether friends see the caller's book reviews in the activity feed. Omit to leave unchanged.</summary>
    public bool? BookReviews { get; set; }
    /// <summary>Whether friends see the caller's film progress in the activity feed. Omit to leave unchanged.</summary>
    public bool? MovieProgress { get; set; }
    /// <summary>Whether friends see the caller's film reviews in the activity feed. Omit to leave unchanged.</summary>
    public bool? MovieReviews { get; set; }
    /// <summary>Whether friends see the caller's series progress in the activity feed. Omit to leave unchanged.</summary>
    public bool? SeriesProgress { get; set; }
    /// <summary>Whether friends see the caller's series reviews in the activity feed. Omit to leave unchanged.</summary>
    public bool? SeriesReviews { get; set; }
}

public class EditUserRequest
{
    /// <summary>New username, 3-50 characters. Omit to keep the current one.</summary>
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    public string? Username { get; set; }

    /// <summary>Feed-privacy settings to change. Only the flags you send are updated.</summary>
    public FeedPrivacyRequest? Privacy { get; set; }
}
