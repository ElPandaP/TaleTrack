using Google.Apis.Auth;
using TaleTrackApp.Features.User;

namespace TaleTrackApp.Features.Auth;

/// <summary>Outcome of a Google sign-in attempt.</summary>
public abstract record GoogleLoginResult
{
    /// <summary>The server has no Google client id configured.</summary>
    public sealed record NotConfigured : GoogleLoginResult;

    /// <summary>The id token failed Google's validation.</summary>
    public sealed record InvalidToken : GoogleLoginResult;

    /// <summary>First sign-in: nothing is persisted until the user picks a username.</summary>
    /// <param name="PendingToken">Signed token carrying the verified Google identity, to send back when completing the sign-up.</param>
    /// <param name="Email">Email of the Google account.</param>
    /// <param name="SuggestedUsername">Default username to offer: the Google display name, or the local part of the email.</param>
    public sealed record NewUser(string PendingToken, string Email, string SuggestedUsername) : GoogleLoginResult;

    /// <summary>An account exists (already linked, or just linked by matching email).</summary>
    /// <param name="User">The account to sign in.</param>
    /// <param name="LinkedExistingAccount">True when the Google identity was just linked to an account found by email.</param>
    public sealed record ExistingUser(Model.User User, bool LinkedExistingAccount) : GoogleLoginResult;
}

/// <summary>Outcome of finishing a Google sign-up.</summary>
public abstract record GoogleSignupResult
{
    /// <summary>The pending token is invalid, tampered with or expired.</summary>
    public sealed record InvalidToken : GoogleSignupResult;

    /// <summary>The chosen username already belongs to someone else.</summary>
    public sealed record UsernameTaken : GoogleSignupResult;

    /// <summary>The account is ready: just created, or one that already existed.</summary>
    /// <param name="User">The account to sign in.</param>
    /// <param name="Created">True when the account was created by this call.</param>
    public sealed record Completed(Model.User User, bool Created) : GoogleSignupResult;
}

/// <summary>Verifies a Google id token and decides whether it belongs to an existing account,
/// should be linked to one, or starts a new sign-up.</summary>
public class GoogleAuthService
{
    /// <summary>Account lookups and changes.</summary>
    private readonly UserService _users;
    /// <summary>Pending Google sign-up tokens.</summary>
    private readonly GoogleSignupTokenService _signupTokens;
    /// <summary>Validates the Google id tokens.</summary>
    private readonly GoogleIdTokenValidator _idTokens;
    /// <summary>Logger.</summary>
    private readonly ILogger<GoogleAuthService> _logger;

    /// <summary>Creates the service with its dependencies (resolved by DI).</summary>
    /// <param name="users">Account lookups and creation.</param>
    /// <param name="signupTokens">Issues and validates the pending sign-up tokens.</param>
    /// <param name="idTokens">Validates the Google id tokens.</param>
    /// <param name="logger">Logger.</param>
    public GoogleAuthService(
        UserService users,
        GoogleSignupTokenService signupTokens,
        GoogleIdTokenValidator idTokens,
        ILogger<GoogleAuthService> logger)
    {
        _users = users;
        _signupTokens = signupTokens;
        _idTokens = idTokens;
        _logger = logger;
    }

    /// <summary>
    /// Validates a Google id token against <c>GOOGLE_CLIENT_ID</c> and finds the matching account: by
    /// Google id first, then by email (linking Google to that account). When neither exists, nothing
    /// is created yet and a pending sign-up token is returned instead.
    /// </summary>
    /// <param name="idToken">The id token obtained by the client from Google Sign-In.</param>
    public async Task<GoogleLoginResult> LoginAsync(string idToken)
    {
        var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
        if (string.IsNullOrEmpty(clientId))
            return new GoogleLoginResult.NotConfigured();

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await _idTokens.ValidateAsync(idToken, clientId);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning("Invalid Google token: {Message}", ex.Message);
            return new GoogleLoginResult.InvalidToken();
        }

        var googleId = payload.Subject;
        var email = payload.Email;
        var name = !string.IsNullOrEmpty(payload.Name) ? payload.Name : email.Split('@')[0];

        var user = await _users.GetByGoogleIdAsync(googleId);
        if (user != null)
            return new GoogleLoginResult.ExistingUser(user, LinkedExistingAccount: false);

        user = await _users.GetByEmailAsync(email);
        if (user != null)
        {
            // An account with this email already exists (signed up normally), so Google is linked
            // to it. Safe to do silently: Google already verified this person controls that email
            // address. The frontend tells the user through LinkedExistingAccount.
            await _users.LinkGoogleIdAsync(user.Id, googleId);
            return new GoogleLoginResult.ExistingUser(user, LinkedExistingAccount: true);
        }

        // New sign-up: the account is not created until the user picks a username
        // (POST /api/auth/google/complete).
        return new GoogleLoginResult.NewUser(_signupTokens.Issue(googleId, email), email, name);
    }

    /// <summary>Finishes a sign-up started by <see cref="LoginAsync"/> once the user has picked a username.</summary>
    /// <param name="pendingToken">Token returned in <see cref="GoogleLoginResult.NewUser"/>.</param>
    /// <param name="username">Username chosen by the user.</param>
    public async Task<GoogleSignupResult> CompleteSignupAsync(string pendingToken, string username)
    {
        var identity = _signupTokens.Validate(pendingToken);
        if (identity == null)
            return new GoogleSignupResult.InvalidToken();

        var (googleId, email) = identity.Value;

        // A double submit, a normal sign-up or another Google login for the same person may have
        // created the account in the meantime; that account is used instead of failing.
        var user = await _users.GetByGoogleIdAsync(googleId) ?? await _users.GetByEmailAsync(email);
        if (user != null)
        {
            if (user.GoogleId == null)
                await _users.LinkGoogleIdAsync(user.Id, googleId);
            return new GoogleSignupResult.Completed(user, Created: false);
        }

        if (await _users.UsernameExistsAsync(username))
            return new GoogleSignupResult.UsernameTaken();

        user = await _users.CreateGoogleUserAsync(email, username, googleId);
        return new GoogleSignupResult.Completed(user, Created: true);
    }
}
