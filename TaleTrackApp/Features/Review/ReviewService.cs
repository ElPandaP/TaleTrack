using TaleTrackApp.Data;
using TaleTrackApp.Model;
using Microsoft.EntityFrameworkCore;

namespace TaleTrackApp.Features.Review;

/// <summary>Outcome of editing or deleting a review.</summary>
public enum ReviewResult { Ok, NotFound, Forbidden }

public class ReviewService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(AppDbContext context, ILogger<ReviewService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Model.Review?> GetByIdAsync(Guid id)
    {
        return await _context.Reviews
            .Include(r => r.User)
            .Include(r => r.Media)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<List<Model.Review>> GetByMediaIdAsync(Guid mediaId)
    {
        return await _context.Reviews
            .Where(r => r.MediaId == mediaId)
            .Include(r => r.User)
            .ToListAsync();
    }

    public async Task<List<Model.Review>> GetByUserIdAsync(Guid userId)
    {
        return await _context.Reviews
            .Where(r => r.UserId == userId)
            .Include(r => r.Media)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// One review per (user, media) — a repeat call updates the existing review in place
    /// instead of creating a duplicate (mirrors TrackingEventService.UpsertAsync).
    /// </summary>
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

            _logger.LogInformation($"Review updated (via create) for User {userId}, Media {mediaId}");
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

        _logger.LogInformation($"Review created for User {userId}, Media {mediaId}");
        return review;
    }

    /// <summary>Edits a review; only its author can.</summary>
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
        if (comment != null)
        {
            review.Comment = comment;
        }

        review.UpdatedAt = DateTime.UtcNow;
        _context.Reviews.Update(review);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Review {id} updated successfully");
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

        _logger.LogInformation($"Review {id} deleted successfully");
        return ReviewResult.Ok;
    }
}
