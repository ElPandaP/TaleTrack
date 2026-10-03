namespace TaleTrackApp.Model;

/// <summary>
/// State of a <see cref="Friendship"/>. Persisted by name (see <see cref="Data.AppDbContext"/>),
/// so the names are part of the schema.
/// </summary>
public enum FriendshipStatus
{
    /// <summary>Sent and waiting for the addressee to answer.</summary>
    Pending,

    /// <summary>Accepted: both users are friends.</summary>
    Accepted
}
