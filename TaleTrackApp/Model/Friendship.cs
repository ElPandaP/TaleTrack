namespace TaleTrackApp.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// A directed friend request that becomes a (still directed on disk, but
/// symmetric in meaning) friendship once accepted.
/// Status: "Pending" | "Accepted". A decline just deletes the row.
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
    [StringLength(20)]
    [RegularExpression(@"^(Pending|Accepted)$")]
    public string Status { get; set; } = "Pending";

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RespondedAt { get; set; }

    public User? Requester { get; set; }
    public User? Addressee { get; set; }
}
