namespace TaleTrackApp.Features.Media.GetMediaById;

public class GetMediaByIdResponse
{
    public bool Success { get; set; }
    public required MediaDetailData Data { get; set; }

    public static GetMediaByIdResponse From(MediaDetail detail, Guid userId)
    {
        var (media, reviews, myReview, myTracking, myProgress) = detail;
        var isSeries = media.Type == "Series";

        return new GetMediaByIdResponse
        {
            Success = true,
            Data = new MediaDetailData
            {
                Id = media.Id,
                TitleEN = media.TitleEN,
                TitleES = media.TitleES,
                Type = media.Type,
                Author = media.Author,
                PosterUrl = media.PosterUrl,
                Length = media.Length,
                Isbn = media.Isbn,
                Description = media.Description,
                AvgRating = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : (double?)null,
                ReviewCount = reviews.Count,
                MyProgress = myProgress,
                MyLastEventDate = myTracking?.EventDate,
                MySeason = isSeries ? myTracking?.Season : null,
                MyEpisode = isSeries ? myTracking?.Episode : null,
                SeasonEpisodeCounts = isSeries ? media.SeasonEpisodeCounts : null,
                MyReviewId = myReview?.Id,
                MyRating = myReview?.Rating,
                MyComment = myReview?.Comment,
                Reviews = reviews
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new MediaReviewItem
                    {
                        Id = r.Id,
                        Rating = r.Rating,
                        Comment = r.Comment,
                        CreatedAt = r.CreatedAt,
                        Username = r.User?.Username,
                        Mine = r.UserId == userId,
                    })
                    .ToList(),
            }
        };
    }
}

public class MediaDetailData
{
    public Guid Id { get; set; }
    public string? TitleEN { get; set; }
    public string? TitleES { get; set; }
    public required string Type { get; set; }
    public string? Author { get; set; }
    public string? PosterUrl { get; set; }
    public int Length { get; set; }
    public string? Isbn { get; set; }
    public string? Description { get; set; }
    public double? AvgRating { get; set; }
    public int ReviewCount { get; set; }
    public int? MyProgress { get; set; }
    public DateTime? MyLastEventDate { get; set; }
    public int? MySeason { get; set; }
    public int? MyEpisode { get; set; }
    public int[]? SeasonEpisodeCounts { get; set; }
    public Guid? MyReviewId { get; set; }
    public int? MyRating { get; set; }
    public string? MyComment { get; set; }
    public required List<MediaReviewItem> Reviews { get; set; }
}

public class MediaReviewItem
{
    public Guid Id { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Username { get; set; }
    public bool Mine { get; set; }
}
