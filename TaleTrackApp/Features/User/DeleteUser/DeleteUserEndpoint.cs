using System.Security.Claims;
using TaleTrackApp.Features.User;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.User.DeleteUser;

public static class DeleteUserEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/user/{id}", HandleAsync)
            .WithName("DeleteUser")
            .WithDescription("Elimina un usuario (requiere JWT + API Key interna)")
            .RequireAuthorization(Policies.UserPolicy)
            .RequireAuthorization(Policies.InternalOnly);
    }

    private static async Task<IResult> HandleAsync(
        int id,
        UserService userService,
        ClaimsPrincipal user,
        ILogger<int> logger)
    {
        // Obtener el UserId del JWT
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        // Solo el usuario puede eliminar su propia cuenta
        if (userId != id)
        {
            logger.LogWarning($"User {userId} tried to delete user {id}");
            return Results.Forbid();
        }

        try
        {
            var success = await userService.DeleteUserAsync(id);
            
            if (!success)
            {
                return Results.NotFound(new { success = false, message = "Usuario no encontrado" });
            }

            logger.LogInformation($"User {id} deleted successfully");
            return Results.Ok(new { success = true, message = "Usuario eliminado exitosamente" });
        }
        catch (Exception ex)
        {
            logger.LogError($"Error deleting user: {ex.Message}");
            return Results.StatusCode(500);
        }
    }
}
