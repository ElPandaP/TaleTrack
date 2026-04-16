namespace TaleTrackApp.Features.User.Login;

using System.ComponentModel.DataAnnotations;

public class LoginRequest
{
    [Required(ErrorMessage = "Email es requerido")]
    [EmailAddress(ErrorMessage = "Email debe ser válido")]
    public required string Email { get; set; }
    
    [Required(ErrorMessage = "Contraseña es requerida")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
    public required string Password { get; set; }
}
