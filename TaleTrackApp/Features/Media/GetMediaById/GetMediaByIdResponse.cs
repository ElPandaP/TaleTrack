namespace TaleTrackApp.Features.Media.GetMediaById;

public class GetMediaByIdResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>The media detail.</summary>
    public required MediaDetailData Data { get; set; }

    public static GetMediaByIdResponse From(MediaDetail detail, Guid userId)
    {
        var (media, reviews, myReview, myTracking, myProgress) = detail;
        var isSeries = media.Type == Model.MediaType.Series;

        return new GetMediaByIdResponse
        {
            Success = true,
            Data = new MediaDetailData
            {
                Id = media.Id,
                TitleEN = media.TitleEN,
                TitleES = media.TitleES,
                Type = media.Type.ToString(),
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
    /// <summary>Media id.</summary>
    public Guid Id { get; set; }
    /// <summary>English title, if known.</summary>
    public string? TitleEN { get; set; }
    /// <summary>Spanish title, if known.</summary>
    public string? TitleES { get; set; }
    /// <summary>`Movie`, `Series` or `Book`.</summary>
    public required string Type { get; set; }
    /// <summary>Author of a book. Absent for films and series.</summary>
    public string? Author { get; set; }
    /// <summary>Poster or cover image URL, once it has been fetched.</summary>
    public string? PosterUrl { get; set; }
    /// <summary>Runtime in minutes for a film, page count for a book, 0 for a series.</summary>
    public int Length { get; set; }
    /// <summary>ISBN of a book, if known.</summary>
    public string? Isbn { get; set; }
    /// <summary>Synopsis, once it has been fetched.</summary>
    public string? Description { get; set; }
    /// <summary>Average rating from 1 to 10, one decimal. Null when there are no reviews.</summary>
    public double? AvgRating { get; set; }
    /// <summary>Number of reviews.</summary>
    public int ReviewCount { get; set; }
    /// <summary>The caller's progress, 0-100. Null when the caller does not track it.</summary>
    public int? MyProgress { get; set; }
    /// <summary>When the caller last reported progress.</summary>
    public DateTime? MyLastEventDate { get; set; }
    /// <summary>Series only: season of the furthest episode the caller reached.</summary>
    public int? MySeason { get; set; }
    /// <summary>Series only: episode of the furthest episode the caller reached.</summary>
    public int? MyEpisode { get; set; }
    /// <summary>Series only: number of episodes of each season, in order. Used to turn season and episode into a percentage.</summary>
    public int[]? SeasonEpisodeCounts { get; set; }
    /// <summary>Id of the caller's review, if they wrote one.</summary>
    public Guid? MyReviewId { get; set; }
    /// <summary>The caller's rating, 1-10.</summary>
    public int? MyRating { get; set; }
    /// <summary>The caller's review comment.</summary>
    public string? MyComment { get; set; }
    /// <summary>Every user's reviews, newest first.</summary>
    public required List<MediaReviewItem> Reviews { get; set; }
}

public class MediaReviewItem
{
    /// <summary>Review id.</summary>
    public Guid Id { get; set; }
    /// <summary>Rating from 1 to 10.</summary>
    public int Rating { get; set; }
    /// <summary>Free-text comment, if any.</summary>
    public string? Comment { get; set; }
    /// <summary>When the review was written.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>Author's username.</summary>
    public string? Username { get; set; }
    /// <summary>True when the caller wrote this review.</summary>
    public bool Mine { get; set; }
}
