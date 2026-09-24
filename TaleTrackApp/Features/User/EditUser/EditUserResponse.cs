namespace TaleTrackApp.Features.User.EditUser;

public class EditUserResponse
{
    /// <summary>Always <c>true</c>.</summary>
    public bool Success { get; set; }

    /// <summary>Human-readable confirmation in English.</summary>
    public required string Message { get; set; }

    /// <summary>
    /// A freshly issued access token. The token embeds username and email, so a client that caches
    /// them from it must replace the old token with this one.
    /// </summary>
    public required string Token { get; set; }

    /// <summary>The user after the update.</summary>
    public required EditedUserData Data { get; set; }
}

public class EditedUserData
{
    /// <summary>User id.</summary>
    public Guid Id { get; set; }

    /// <summary>Username after the update.</summary>
    public required string Username { get; set; }

    /// <summary>Account email (not editable here).</summary>
    public required string Email { get; set; }

    /// <summary>When the profile was last updated.</summary>
    public DateTime? UpdatedAt { get; set; }
}
