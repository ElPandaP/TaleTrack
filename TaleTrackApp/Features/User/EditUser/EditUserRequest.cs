namespace TaleTrackApp.Features.User.EditUser;

using System.ComponentModel.DataAnnotations;

public class EditUserRequest
{
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username debe tener entre 3 y 50 caracteres")]
    public string? Username { get; set; }
    
    [EmailAddress(ErrorMessage = "Email inválido")]
    [StringLength(256, ErrorMessage = "Email no puede exceder 256 caracteres")]
    public string? Email { get; set; }
    
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password debe tener entre 6 y 100 caracteres")]
    public string? Password { get; set; }
}
