namespace TaleTrackApp.Features.User.EditUser;

using System.ComponentModel.DataAnnotations;

public class FeedPrivacyRequest
{
    public bool? BookProgress { get; set; }
    public bool? BookReviews { get; set; }
    public bool? MovieProgress { get; set; }
    public bool? MovieReviews { get; set; }
    public bool? SeriesProgress { get; set; }
    public bool? SeriesReviews { get; set; }
}

public class EditUserRequest
{
    [StringLength(50, MinimumLength = 3, ErrorMessage = "El nombre de usuario debe tener entre 3 y 50 caracteres")]
    public string? Username { get; set; }

    [EmailAddress(ErrorMessage = "Email debe ser válido")]
    [StringLength(256, ErrorMessage = "El email no puede superar los 256 caracteres")]
    public string? Email { get; set; }

    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 100 caracteres")]
    public string? Password { get; set; }

    [StringLength(2048, ErrorMessage = "La URL del avatar no puede superar los 2048 caracteres")]
    [Url(ErrorMessage = "La URL del avatar debe ser válida")]
    public string? AvatarUrl { get; set; }

    public FeedPrivacyRequest? Privacy { get; set; }
}
