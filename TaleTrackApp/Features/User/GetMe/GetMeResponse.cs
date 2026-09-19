namespace TaleTrackApp.Features.User.GetMe;

public class GetMeResponse
{
    public bool Success { get; set; }
    public required MeData Data { get; set; }
}

public class MeData
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public required FeedPrivacyData Privacy { get; set; }
}

public class FeedPrivacyData
{
    public bool BookProgress { get; set; }
    public bool BookReviews { get; set; }
    public bool MovieProgress { get; set; }
    public bool MovieReviews { get; set; }
    public bool SeriesProgress { get; set; }
    public bool SeriesReviews { get; set; }
}
