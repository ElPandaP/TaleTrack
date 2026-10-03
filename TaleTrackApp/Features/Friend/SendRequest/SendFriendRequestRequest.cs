using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Friend.SendRequest;

/// <summary>Body of <c>POST /api/friends/requests</c>.</summary>
public class SendFriendRequestRequest
{
    /// <summary>Id of the user to befriend. Look it up with `GET /api/users/search`.</summary>
    [Required(ErrorMessage = "userId is required")]
    public Guid? UserId { get; set; }
}
