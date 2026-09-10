using System.Security.Claims;
using TaleTrackApp.Features.User;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.User.EditUser;

public static class EditUserEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/user/{id}", HandleAsync)
            .WithName("EditUser")
            .WithDescription("Edit a user (requires JWT + internal API key)")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy)
            .RequireAuthorization(Policies.InternalOnly);
    }

    private static async Task<IResult> HandleAsync(
        int id,
        EditUserRequest request,
        UserService userService,
        ClaimsPrincipal user,
        ILogger<EditUserRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        // A user may only edit their own account
        if (userId != id)
        {
            logger.LogWarning($"User {userId} tried to edit user {id}");
            return Results.Forbid();
        }

        try
        {
            var privacy = request.Privacy == null ? null : new FeedPrivacy(
                request.Privacy.BookProgress, request.Privacy.BookReviews,
                request.Privacy.MovieProgress, request.Privacy.MovieReviews,
                request.Privacy.SeriesProgress, request.Privacy.SeriesReviews);

            var updatedUser = await userService.UpdateUserAsync(
                id, request.Username, request.Email, request.Password, request.AvatarUrl, privacy);
            
            if (updatedUser == null)
            {
                return Results.NotFound(new { success = false, message = "Usuario no encontrado." });
            }

            logger.LogInformation($"User {id} updated successfully");
            return Results.Ok(new 
            { 
                success = true,
                message = "Usuario actualizado exitosamente.",
                data = new
                {
                    id = updatedUser.Id,
                    username = updatedUser.Username,
                    email = updatedUser.Email,
                    updatedAt = updatedUser.UpdatedAt
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError($"Error updating user: {ex.Message}");
            return Results.StatusCode(500);
        }
    }
}
