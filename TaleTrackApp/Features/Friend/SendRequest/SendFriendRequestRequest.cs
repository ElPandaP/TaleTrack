using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Friend.SendRequest;

public class SendFriendRequestRequest
{
    /// <summary>Id of the user to befriend. Look it up with `GET /api/users/search`.</summary>
    [Required(ErrorMessage = "userId is required")]
    public Guid? UserId { get; set; }
}
