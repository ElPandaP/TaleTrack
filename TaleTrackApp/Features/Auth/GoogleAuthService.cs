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
    public sealed record NewUser(string PendingToken, string Email, string SuggestedUsername) : GoogleLoginResult;

    /// <summary>An account exists (already linked, or just linked by matching email).</summary>
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
    public sealed record Completed(Model.User User, bool Created) : GoogleSignupResult;
}

/// <summary>Verifies a Google id token and decides whether it belongs to an existing account,
/// should be linked to one, or starts a new sign-up.</summary>
public class GoogleAuthService
{
    private readonly UserService _users;
    private readonly GoogleSignupTokenService _signupTokens;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(
        UserService users,
        GoogleSignupTokenService signupTokens,
        ILogger<GoogleAuthService> logger)
    {
        _users = users;
        _signupTokens = signupTokens;
        _logger = logger;
    }

    public async Task<GoogleLoginResult> LoginAsync(string idToken)
    {
        var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
        if (string.IsNullOrEmpty(clientId))
            return new GoogleLoginResult.NotConfigured();

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId]
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
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
            // An account with this email already exists (signed up normally) — link
            // Google to it. Safe to do silently: Google already verified this person
            // controls that email address. The frontend surfaces this to the user.
            await _users.LinkGoogleIdAsync(user.Id, googleId);
            return new GoogleLoginResult.ExistingUser(user, LinkedExistingAccount: true);
        }

        // Brand new signup — don't create the account yet, the user needs to pick a
        // username first. Nothing is persisted until /auth/google/complete.
        return new GoogleLoginResult.NewUser(_signupTokens.Issue(googleId, email, name), email, name);
    }

    /// <summary>Finishes a sign-up started by <see cref="LoginAsync"/> once the user has picked a username.</summary>
    public async Task<GoogleSignupResult> CompleteSignupAsync(string pendingToken, string username)
    {
        var identity = _signupTokens.Validate(pendingToken);
        if (identity == null)
            return new GoogleSignupResult.InvalidToken();

        var (googleId, email, _) = identity.Value;

        // Defensive re-check: a double-submit (or a normal signup / another Google login for
        // the same person in the meantime) may have created the account already — use it
        // instead of failing.
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
