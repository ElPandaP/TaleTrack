using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.User;
using TaleTrackApp.Security;
using TaleTrackApp.Services;

namespace TaleTrackApp.Features.User.EditUser;

public static class EditUserEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/users/{id:guid}", HandleAsync)
            .WithName("EditUser")
            .WithTags("Users")
            .WithSummary("Edit the caller's profile")
            .WithDescription("Partial update: only the fields you send change. The response carries a freshly issued access token, because the token embeds the username; replace the stored one.")
            .Responds<EditUserResponse>("Profile updated.")
            .RespondsBadRequest("Validation failed, or the code is `username_taken`.")
            .Responds(StatusCodes.Status403Forbidden, "The id in the path is not the caller's; users can only edit themselves.")
            .RespondsNotFound("The user does not exist.")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        EditUserRequest request,
        UserService userService,
        JwtService jwtService,
        ClaimsPrincipal user,
        ILogger<EditUserRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
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

            var (result, updatedUser) = await userService.UpdateUserAsync(
                id, request.Username, privacy);

            switch (result)
            {
                case UpdateUserResult.NotFound:
                    return Results.NotFound(new { success = false, message = "User not found." });
                case UpdateUserResult.UsernameTaken:
                    return Results.BadRequest(new { success = false, code = "username_taken", message = "Username already taken" });
            }

            logger.LogInformation($"User {id} updated successfully");

            // The access token carries username/email as claims — reissue it so the client's
            // cached auth state doesn't keep showing stale values until it naturally expires.
            var token = jwtService.GenerateToken(updatedUser!.Id, updatedUser.Email, updatedUser.Username);

            return Results.Ok(new EditUserResponse
            {
                Success = true,
                Message = "User updated successfully.",
                Token = token,
                Data = new EditedUserData
                {
                    Id = updatedUser.Id,
                    Username = updatedUser.Username,
                    Email = updatedUser.Email,
                    UpdatedAt = updatedUser.UpdatedAt,
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
