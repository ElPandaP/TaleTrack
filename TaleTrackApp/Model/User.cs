namespace TaleTrackApp.Model;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// A registered account. A user signs in with a password, with Google (<see cref="GoogleId"/>) or
/// with a one-time code sent by email, and owns their reviews, tracking, friendships and sessions.
/// </summary>
public class User
{
    /// <summary>Primary key; also the <c>sub</c> claim of the user's access tokens.</summary>
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();
    
    /// <summary>Unique email address of the account.</summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
    public required string Email { get; set; }
    
    /// <summary>Unique public name, 3 to 50 characters.</summary>
    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    public required string Username { get; set; }
    
    /// <summary>Salted PBKDF2 hash of the password; null for accounts without a password (e.g. Google-only).</summary>
    [StringLength(512, ErrorMessage = "Password hash cannot exceed 512 characters")]
    public string? PasswordHash { get; set; }

    /// <summary>URL of the uploaded profile photo, with a version query that changes on every upload; null when there is none.</summary>
    [StringLength(2048)]
    public string? AvatarUrl { get; set; }

    // Feed privacy, per media type: what friends see of this user's activity.

    /// <summary>Whether friends see this user's book progress in their activity feed.</summary>
    public bool ShareBookProgress { get; set; } = true;

    /// <summary>Whether friends see this user's book reviews in their activity feed.</summary>
    public bool ShareBookReviews { get; set; } = true;

    /// <summary>Whether friends see this user's film progress in their activity feed.</summary>
    public bool ShareMovieProgress { get; set; } = true;

    /// <summary>Whether friends see this user's film reviews in their activity feed.</summary>
    public bool ShareMovieReviews { get; set; } = true;

    /// <summary>Whether friends see this user's series progress in their activity feed.</summary>
    public bool ShareSeriesProgress { get; set; } = true;

    /// <summary>Whether friends see this user's series reviews in their activity feed.</summary>
    public bool ShareSeriesReviews { get; set; } = true;

    /// <summary>Google account id (the <c>sub</c> of Google's id token) once Google sign-in is linked; null otherwise.</summary>
    [StringLength(255, ErrorMessage = "Google ID cannot exceed 255 characters")]
    public string? GoogleId { get; set; }

    /// <summary>Current one-time sign-in code sent by email; null when none is pending.</summary>
    [StringLength(6, ErrorMessage = "Email code cannot exceed 6 characters")]
    public string? EmailCode { get; set; }

    /// <summary>When <see cref="EmailCode"/> expires (UTC).</summary>
    public DateTime? EmailCodeExpiry { get; set; }

    /// <summary>Wrong guesses against the current email code; the code is discarded after too many.</summary>
    public int EmailCodeFailedAttempts { get; set; }

    /// <summary>When the account was created (UTC).</summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Last profile change (UTC); null if never changed.</summary>
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>Reviews written by the user.</summary>
    public ICollection<Review> Reviews { get; set; } = new List<Review>();

    /// <summary>The user's progress on every media they track.</summary>
    public ICollection<TrackingEvent> TrackingEvents { get; set; } = new List<TrackingEvent>();
}
