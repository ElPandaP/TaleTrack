using TaleTrackApp.Data;
using TaleTrackApp.Model;
using Microsoft.EntityFrameworkCore;

namespace TaleTrackApp.Features.Review;

/// <summary>Outcome of editing or deleting a review: done, the review does not exist, or it belongs to another user (<c>Forbidden</c>).</summary>
public enum ReviewResult { Ok, NotFound, Forbidden }

/// <summary>
/// Reads and writes reviews. A user has at most one review per media, and only its author can edit
/// or delete it.
/// </summary>
public class ReviewService
{
    /// <summary>Database context used to read and write reviews.</summary>
    private readonly AppDbContext _context;
    /// <summary>Logger for this service.</summary>
    private readonly ILogger<ReviewService> _logger;

    /// <summary>Creates the service with its database context and logger.</summary>
    public ReviewService(AppDbContext context, ILogger<ReviewService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Every review of a media, with their authors loaded.</summary>
    public async Task<List<Model.Review>> GetByMediaIdAsync(Guid mediaId)
    {
        return await _context.Reviews
            .Where(r => r.MediaId == mediaId)
            .Include(r => r.User)
            .ToListAsync();
    }

    /// <summary>Every review written by a user, newest first, with their media loaded.</summary>
    public async Task<List<Model.Review>> GetByUserIdAsync(Guid userId)
    {
        return await _context.Reviews
            .Where(r => r.UserId == userId)
            .Include(r => r.Media)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Saves the user's review of a media. A user has one review per media, so if one already
    /// exists its rating and comment are overwritten instead of creating a second one.
    /// </summary>
    /// <returns>The saved review.</returns>
    public async Task<Model.Review> CreateAsync(Guid userId, Guid mediaId, int rating, string? comment = null)
    {
        var existing = await _context.Reviews
            .FirstOrDefaultAsync(r => r.UserId == userId && r.MediaId == mediaId);

        if (existing != null)
        {
            existing.Rating = rating;
            existing.Comment = comment;
            existing.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Review updated (via create) for User {UserId}, Media {MediaId}", userId, mediaId);
            return existing;
        }

        var review = new Model.Review
        {
            UserId = userId,
            MediaId = mediaId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTime.UtcNow
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Review created for User {UserId}, Media {MediaId}", userId, mediaId);
        return review;
    }

    /// <summary>
    /// Edits a review; only its author can. The rating and the comment are both replaced, so a
    /// null <paramref name="comment"/> clears it.
    /// </summary>
    /// <returns>The outcome and, when it is <see cref="ReviewResult.Ok"/>, the updated review.</returns>
    public async Task<(ReviewResult Result, Model.Review? Review)> UpdateAsync(
        Guid userId, Guid id, int rating, string? comment = null)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null)
        {
            return (ReviewResult.NotFound, null);
        }

        if (review.UserId != userId)
        {
            _logger.LogWarning("User {UserId} tried to edit review {ReviewId} owned by {OwnerId}",
                userId, id, review.UserId);
            return (ReviewResult.Forbidden, null);
        }

        review.Rating = rating;
        review.Comment = comment;

        review.UpdatedAt = DateTime.UtcNow;
        _context.Reviews.Update(review);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Review {ReviewId} updated", id);
        return (ReviewResult.Ok, review);
    }

    /// <summary>Deletes a review; only its author can.</summary>
    public async Task<ReviewResult> DeleteAsync(Guid userId, Guid id)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null)
        {
            return ReviewResult.NotFound;
        }

        if (review.UserId != userId)
        {
            _logger.LogWarning("User {UserId} tried to delete review {ReviewId} owned by {OwnerId}",
                userId, id, review.UserId);
            return ReviewResult.Forbidden;
        }

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Review {ReviewId} deleted", id);
        return ReviewResult.Ok;
    }
}
