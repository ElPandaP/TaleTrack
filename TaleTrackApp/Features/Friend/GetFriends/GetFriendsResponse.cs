using TaleTrackApp.Features.Friend;

namespace TaleTrackApp.Features.Friend.GetFriends;

public class GetFriendsResponse
{
    public bool Success { get; set; }
    public required List<FriendDto> Friends { get; set; }
    public required List<FriendRequestDto> Incoming { get; set; }
    public required List<FriendRequestDto> Outgoing { get; set; }
}
