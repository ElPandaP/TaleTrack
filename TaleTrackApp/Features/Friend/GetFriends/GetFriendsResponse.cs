using TaleTrackApp.Features.Friend;

namespace TaleTrackApp.Features.Friend.GetFriends;

public class GetFriendsResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>Accepted friends.</summary>
    public required List<FriendItem> Friends { get; set; }
    /// <summary>Requests the caller can accept or decline.</summary>
    public required List<FriendRequestItem> Incoming { get; set; }
    /// <summary>Requests the caller sent that are still pending.</summary>
    public required List<FriendRequestItem> Outgoing { get; set; }
}
