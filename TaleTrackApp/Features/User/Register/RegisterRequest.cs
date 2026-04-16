namespace TaleTrackApp.Features.User.Register;

using System.ComponentModel.DataAnnotations;

public class RegisterRequest
{
    [Required(ErrorMessage = "Email es requerido")]
    [EmailAddress(ErrorMessage = "Email debe ser válido")]
    public required string Email { get; set; }
    
    [Required(ErrorMessage = "Username es requerido")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username debe tener entre 3 y 50 caracteres")]
    public required string Username { get; set; }
    
    [Required(ErrorMessage = "Contraseña es requerida")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
    public required string Password { get; set; }
}
