namespace TaleTrackApp.Features.Auth;

/// <summary>
/// Rule every new password must follow, at registration and when resetting it: 8 to 100
/// characters, with at least one uppercase letter and one digit.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>Regular expression a valid password matches.</summary>
    public const string Pattern = @"^(?=.*\p{Lu})(?=.*\d).{8,100}$";

    /// <summary>Validation message returned when a password does not follow the rule.</summary>
    public const string Message =
        "Password must be 8 to 100 characters long and include an uppercase letter and a number";
}
