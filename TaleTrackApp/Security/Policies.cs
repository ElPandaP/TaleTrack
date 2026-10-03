namespace TaleTrackApp.Security;

/// <summary>Names of the authorization policies registered in <c>Program.cs</c>.</summary>
public static class Policies
{
    /// <summary>
    /// Requires a valid access token (JWT). Every endpoint that works with the caller's own data uses
    /// it; it is the only access control of the API, which is otherwise public.
    /// </summary>
    public const string UserPolicy = "UserPolicy";
}
