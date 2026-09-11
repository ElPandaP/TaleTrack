using TaleTrackApp.Data;
using TaleTrackApp.Model;
using Microsoft.EntityFrameworkCore;

namespace TaleTrackApp.Features.Review;

public class ReviewService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(AppDbContext context, ILogger<ReviewService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Model.Review?> GetByIdAsync(int id)
    {
        return await _context.Reviews
            .Include(r => r.User)
            .Include(r => r.Media)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<List<Model.Review>> GetByMediaIdAsync(int mediaId)
    {
        return await _context.Reviews
            .Where(r => r.MediaId == mediaId)
            .Include(r => r.User)
            .ToListAsync();
    }

    public async Task<List<Model.Review>> GetByUserIdAsync(int userId)
    {
        return await _context.Reviews
            .Where(r => r.UserId == userId)
            .Include(r => r.Media)
            .ToListAsync();
    }

    /// <summary>
    /// One review per (user, media) — a repeat call updates the existing review in place
    /// instead of creating a duplicate (mirrors TrackingEventService.UpsertAsync).
    /// </summary>
    public async Task<Model.Review> CreateAsync(int userId, int mediaId, int rating, string? comment = null)
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

    public async Task<Model.Review?> UpdateAsync(int id, int rating, string? comment = null)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null)
        {
            return null;
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
        return review;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null)
        {
            return false;
        }

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Review {id} deleted successfully");
        return true;
    }
}
