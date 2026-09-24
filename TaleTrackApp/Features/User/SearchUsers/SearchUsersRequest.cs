using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.User.SearchUsers;

public class SearchUsersRequest
{
    /// <summary>Exact username, with or without a leading @.</summary>
    [Required(ErrorMessage = "username is required")]
    [StringLength(50, MinimumLength = 1)]
    public required string Username { get; set; }
}
