using TaleTrackApp.Data;
using TaleTrackApp.Model;
using Microsoft.EntityFrameworkCore;

namespace TaleTrackApp.Features.Friend;

public record FriendDto(int UserId, string Username, string? AvatarUrl);
public record FriendRequestDto(int RequestId, int UserId, string Username, string? AvatarUrl, DateTime CreatedAt);

public enum SendRequestResult { Ok, TargetNotFound, Self, AlreadyFriends, AlreadyPending, ReversePending }
public enum RespondResult { Ok, NotFound, Forbidden }

public class FriendService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FriendService> _logger;

    public FriendService(AppDbContext context, ILogger<FriendService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Accepted friendships (either direction) as the "other" user.</summary>
    public async Task<List<FriendDto>> GetFriendsAsync(int userId)
    {
        var rows = await _context.Friendships
            .Where(f => f.Status == "Accepted" && (f.RequesterId == userId || f.AddresseeId == userId))
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .ToListAsync();

        return rows
            .Select(f => f.RequesterId == userId ? f.Addressee! : f.Requester!)
            .Select(u => new FriendDto(u.Id, u.Username, u.AvatarUrl))
            .OrderBy(f => f.Username)
            .ToList();
    }

    /// <summary>Pending requests addressed to the user.</summary>
    public async Task<List<FriendRequestDto>> GetIncomingAsync(int userId)
    {
        var rows = await _context.Friendships
            .Where(f => f.Status == "Pending" && f.AddresseeId == userId)
            .Include(f => f.Requester)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return rows
            .Select(f => new FriendRequestDto(f.Id, f.Requester!.Id, f.Requester.Username, f.Requester.AvatarUrl, f.CreatedAt))
            .ToList();
    }

    /// <summary>Pending requests the user has sent.</summary>
    public async Task<List<FriendRequestDto>> GetOutgoingAsync(int userId)
    {
        var rows = await _context.Friendships
            .Where(f => f.Status == "Pending" && f.RequesterId == userId)
            .Include(f => f.Addressee)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return rows
            .Select(f => new FriendRequestDto(f.Id, f.Addressee!.Id, f.Addressee.Username, f.Addressee.AvatarUrl, f.CreatedAt))
            .ToList();
    }

    public async Task<(SendRequestResult Result, Model.User? Target)> SendRequestAsync(int userId, int targetId)
    {
        var target = await _context.Users.FindAsync(targetId);
        if (target == null) return (SendRequestResult.TargetNotFound, null);
        if (target.Id == userId) return (SendRequestResult.Self, target);

        var existing = await _context.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterId == userId && f.AddresseeId == target.Id) ||
            (f.RequesterId == target.Id && f.AddresseeId == userId));

        if (existing != null)
        {
            if (existing.Status == "Accepted") return (SendRequestResult.AlreadyFriends, target);
            return existing.RequesterId == userId
                ? (SendRequestResult.AlreadyPending, target)
                : (SendRequestResult.ReversePending, target);
        }

        _context.Friendships.Add(new Model.Friendship
        {
            RequesterId = userId,
            AddresseeId = target.Id,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
        _logger.LogInformation("Friend request {From} -> {To}", userId, target.Id);
        return (SendRequestResult.Ok, target);
    }

    public async Task<RespondResult> RespondAsync(int userId, int requestId, bool accept)
    {
        var req = await _context.Friendships.FindAsync(requestId);
        if (req == null || req.Status != "Pending") return RespondResult.NotFound;
        if (req.AddresseeId != userId) return RespondResult.Forbidden;

        if (accept)
        {
            req.Status = "Accepted";
            req.RespondedAt = DateTime.UtcNow;
        }
        else
        {
            _context.Friendships.Remove(req);
        }
        await _context.SaveChangesAsync();
        return RespondResult.Ok;
    }

    /// <summary>Removes any friendship or pending request between the two users.</summary>
    public async Task<bool> RemoveAsync(int userId, int otherUserId)
    {
        var rows = await _context.Friendships
            .Where(f =>
                (f.RequesterId == userId && f.AddresseeId == otherUserId) ||
                (f.RequesterId == otherUserId && f.AddresseeId == userId))
            .ToListAsync();

        if (rows.Count == 0) return false;
        _context.Friendships.RemoveRange(rows);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>Relationship of <paramref name="otherUserId"/> to the viewer.</summary>
    public async Task<string> RelationshipAsync(int userId, int otherUserId)
    {
        if (userId == otherUserId) return "self";
        var f = await _context.Friendships.FirstOrDefaultAsync(x =>
            (x.RequesterId == userId && x.AddresseeId == otherUserId) ||
            (x.RequesterId == otherUserId && x.AddresseeId == userId));
        if (f == null) return "none";
        if (f.Status == "Accepted") return "friends";
        return f.RequesterId == userId ? "outgoing" : "incoming";
    }
}
