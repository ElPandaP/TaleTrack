using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Friend.RespondRequest;

public class RespondFriendRequestRequest
{
    [Required(ErrorMessage = "accept is required")]
    public bool? Accept { get; set; }
}
