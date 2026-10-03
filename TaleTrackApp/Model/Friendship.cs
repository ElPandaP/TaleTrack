namespace TaleTrackApp.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// A directed friend request that becomes a (still directed on disk, but
/// symmetric in meaning) friendship once accepted. A decline just deletes the row.
/// There is at most one row per pair of users, in either direction (see <see cref="Data.AppDbContext"/>).
/// </summary>
public class Friendship
{
    /// <summary>Primary key; also the id of the request in the friend-request endpoints.</summary>
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>User who sent the request.</summary>
    [Required]
    [ForeignKey(nameof(Requester))]
    public Guid RequesterId { get; set; }

    /// <summary>User who received the request.</summary>
    [Required]
    [ForeignKey(nameof(Addressee))]
    public Guid AddresseeId { get; set; }

    /// <summary>Whether the request is still pending or has been accepted.</summary>
    [Required]
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

    /// <summary>When the request was sent (UTC).</summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the request was accepted (UTC); null while pending.</summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>Navigation to the sender.</summary>
    public User? Requester { get; set; }

    /// <summary>Navigation to the receiver.</summary>
    public User? Addressee { get; set; }
}
