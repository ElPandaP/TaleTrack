using TaleTrackApp.Data;
using TaleTrackApp.Model;
using Microsoft.EntityFrameworkCore;

namespace TaleTrackApp.Features.Friend;

/// <summary>An accepted friend.</summary>
/// <param name="UserId">The friend's user id.</param>
/// <param name="Username">The friend's public username.</param>
/// <param name="AvatarUrl">The friend's profile photo URL. Null when they have none.</param>
public record FriendItem(Guid UserId, string Username, string? AvatarUrl);

/// <summary>A pending friend request, seen from the other user's side.</summary>
/// <param name="RequestId">Request id. Pass it to the accept and decline endpoints.</param>
/// <param name="UserId">The other user's id: the sender of an incoming request, the recipient of an outgoing one.</param>
/// <param name="Username">The other user's public username.</param>
/// <param name="AvatarUrl">The other user's profile photo URL. Null when they have none.</param>
/// <param name="CreatedAt">When the request was sent.</param>
public record FriendRequestItem(Guid RequestId, Guid UserId, string Username, string? AvatarUrl, DateTime CreatedAt);

/// <summary>
/// Outcome of <see cref="FriendService.SendRequestAsync"/>: the request was created (<c>Ok</c>),
/// the target does not exist, is the sender (<c>Self</c>), is already a friend, already has a
/// pending request from the sender (<c>AlreadyPending</c>) or has already sent one to the sender
/// (<c>ReversePending</c>).
/// </summary>
public enum SendRequestResult { Ok, TargetNotFound, Self, AlreadyFriends, AlreadyPending, ReversePending }
/// <summary>
/// Outcome of accepting or declining a friend request: done, no pending request with that id
/// (<c>NotFound</c>), or the request is addressed to someone else (<c>Forbidden</c>).
/// </summary>
public enum RespondResult { Ok, NotFound, Forbidden }

/// <summary>
/// Manages friendships: sending, accepting and declining requests, removing friends and telling
/// how two users relate. A pair of users has at most one friendship row, in either direction.
/// </summary>
public class FriendService
{
    /// <summary>Database context used to read and write friendships.</summary>
    private readonly AppDbContext _context;
    /// <summary>Logger for this service.</summary>
    private readonly ILogger<FriendService> _logger;

    /// <summary>Creates the service with its database context and logger.</summary>
    public FriendService(AppDbContext context, ILogger<FriendService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>The user's accepted friends, whoever sent the request, ordered by username.</summary>
    public async Task<List<FriendItem>> GetFriendsAsync(Guid userId)
    {
        var rows = await _context.Friendships
            .Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterId == userId || f.AddresseeId == userId))
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .ToListAsync();

        return rows
            .Select(f => f.RequesterId == userId ? f.Addressee! : f.Requester!)
            .Select(u => new FriendItem(u.Id, u.Username, u.AvatarUrl))
            .OrderBy(f => f.Username)
            .ToList();
    }

    /// <summary>Pending requests addressed to the user, newest first.</summary>
    public async Task<List<FriendRequestItem>> GetIncomingAsync(Guid userId)
    {
        var rows = await _context.Friendships
            .Where(f => f.Status == FriendshipStatus.Pending && f.AddresseeId == userId)
            .Include(f => f.Requester)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return rows
            .Select(f => new FriendRequestItem(f.Id, f.Requester!.Id, f.Requester.Username, f.Requester.AvatarUrl, f.CreatedAt))
            .ToList();
    }

    /// <summary>Pending requests the user has sent, newest first.</summary>
    public async Task<List<FriendRequestItem>> GetOutgoingAsync(Guid userId)
    {
        var rows = await _context.Friendships
            .Where(f => f.Status == FriendshipStatus.Pending && f.RequesterId == userId)
            .Include(f => f.Addressee)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return rows
            .Select(f => new FriendRequestItem(f.Id, f.Addressee!.Id, f.Addressee.Username, f.Addressee.AvatarUrl, f.CreatedAt))
            .ToList();
    }

    /// <summary>
    /// Sends a friend request from <paramref name="userId"/> to <paramref name="targetId"/>, unless
    /// the target does not exist, is the user, or there is already a friendship or request between them.
    /// </summary>
    /// <returns>The outcome.</returns>
    public async Task<SendRequestResult> SendRequestAsync(Guid userId, Guid targetId)
    {
        var target = await _context.Users.FindAsync(targetId);
        if (target == null) return SendRequestResult.TargetNotFound;
        if (target.Id == userId) return SendRequestResult.Self;

        var existing = await FindBetweenAsync(userId, target.Id);
        if (existing != null) return Conflict(existing, userId);

        _context.Friendships.Add(new Model.Friendship
        {
            RequesterId = userId,
            AddresseeId = target.Id,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        });
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The other user may have sent theirs between the check above and the write, which
            // the unique index on the pair catches. Any other write failure is not ours to swallow.
            _context.ChangeTracker.Clear();
            existing = await FindBetweenAsync(userId, target.Id);
            if (existing != null) return Conflict(existing, userId);
            throw;
        }

        _logger.LogInformation("Friend request {From} -> {To}", userId, target.Id);
        return SendRequestResult.Ok;
    }

    /// <summary>The friendship row between two users, in either direction, or null if there is none.</summary>
    private Task<Model.Friendship?> FindBetweenAsync(Guid userId, Guid otherUserId) =>
        _context.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterId == userId && f.AddresseeId == otherUserId) ||
            (f.RequesterId == otherUserId && f.AddresseeId == userId));

    /// <summary>Which conflict an existing friendship row means for a new request from <paramref name="userId"/>.</summary>
    private static SendRequestResult Conflict(Model.Friendship existing, Guid userId) =>
        existing.Status == FriendshipStatus.Accepted ? SendRequestResult.AlreadyFriends
        : existing.RequesterId == userId ? SendRequestResult.AlreadyPending
        : SendRequestResult.ReversePending;

    /// <summary>Accepts a pending request addressed to the user.</summary>
    public async Task<RespondResult> AcceptAsync(Guid userId, Guid requestId)
    {
        var (req, error) = await FindPendingForAsync(userId, requestId);
        if (req == null) return error;

        req.Status = FriendshipStatus.Accepted;
        req.RespondedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return RespondResult.Ok;
    }

    /// <summary>Declines a pending request addressed to the user by deleting it, so it can be sent again later.</summary>
    public async Task<RespondResult> DeclineAsync(Guid userId, Guid requestId)
    {
        var (req, error) = await FindPendingForAsync(userId, requestId);
        if (req == null) return error;

        _context.Friendships.Remove(req);
        await _context.SaveChangesAsync();
        return RespondResult.Ok;
    }

    /// <summary>
    /// Loads a pending request that the user may answer. When it cannot be answered the request is
    /// null and the error says why (not found, or addressed to someone else).
    /// </summary>
    private async Task<(Model.Friendship? Request, RespondResult Error)> FindPendingForAsync(Guid userId, Guid requestId)
    {
        var req = await _context.Friendships.FindAsync(requestId);
        if (req == null || req.Status != FriendshipStatus.Pending) return (null, RespondResult.NotFound);
        if (req.AddresseeId != userId) return (null, RespondResult.Forbidden);
        return (req, RespondResult.Ok);
    }

    /// <summary>Removes any friendship or pending request between the two users.</summary>
    /// <returns>False if there was nothing to remove.</returns>
    public async Task<bool> RemoveAsync(Guid userId, Guid otherUserId)
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

    /// <summary>
    /// How <paramref name="otherUserId"/> relates to <paramref name="userId"/>: <c>self</c>,
    /// <c>friends</c>, <c>outgoing</c> (the user asked them), <c>incoming</c> (they asked the user)
    /// or <c>none</c>.
    /// </summary>
    public async Task<string> RelationshipAsync(Guid userId, Guid otherUserId)
    {
        if (userId == otherUserId) return "self";
        var f = await FindBetweenAsync(userId, otherUserId);
        if (f == null) return "none";
        if (f.Status == FriendshipStatus.Accepted) return "friends";
        return f.RequesterId == userId ? "outgoing" : "incoming";
    }
}
