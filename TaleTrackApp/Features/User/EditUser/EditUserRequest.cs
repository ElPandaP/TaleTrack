namespace TaleTrackApp.Features.User.EditUser;

using System.ComponentModel.DataAnnotations;

public class FeedPrivacyRequest
{
    public bool? BookProgress { get; set; }
    public bool? BookReviews { get; set; }
    public bool? MovieProgress { get; set; }
    public bool? MovieReviews { get; set; }
    public bool? SeriesProgress { get; set; }
    public bool? SeriesReviews { get; set; }
}

public class EditUserRequest
{
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    public string? Username { get; set; }

    public FeedPrivacyRequest? Privacy { get; set; }
}
