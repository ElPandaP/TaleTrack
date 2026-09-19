using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Friend.SendRequest;

public class SendFriendRequestRequest
{
    [Required(ErrorMessage = "userId is required")]
    public Guid? UserId { get; set; }
}
