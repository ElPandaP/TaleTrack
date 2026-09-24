namespace TaleTrackApp.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// A directed friend request that becomes a (still directed on disk, but
/// symmetric in meaning) friendship once accepted. A decline just deletes the row.
/// There is at most one row per pair of users, in either direction (see <c>AppDbContext</c>).
/// </summary>
public class Friendship
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [Required]
    [ForeignKey(nameof(Requester))]
    public Guid RequesterId { get; set; }

    [Required]
    [ForeignKey(nameof(Addressee))]
    public Guid AddresseeId { get; set; }

    [Required]
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RespondedAt { get; set; }

    public User? Requester { get; set; }
    public User? Addressee { get; set; }
}
