using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.User;
using TaleTrackApp.Security;
using TaleTrackApp.Services;

namespace TaleTrackApp.Features.User.EditUser;

/// <summary>
/// <c>PUT /api/users/{id}</c>: partial update of the caller's profile (username and feed-privacy
/// flags). Users can only edit themselves. Requires a valid JWT.
/// Applies the changes and returns the updated profile with a new access token. Returns 403 if the id is not the caller's, 404 if the user does not exist, 400 with <c>username_taken</c> if the new username is in use and 401 if the token has no valid user id.
/// </summary>
public static class EditUserEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, with request validation and the user policy.</summary>
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

    /// <summary>Edit the caller's profile</summary>
    private static async Task<IResult> HandleAsync(
        Guid id,
        EditUserRequest request,
        UserService userService,
        JwtService jwtService,
        ClaimsPrincipal user,
        ILogger<EditUserRequest> logger)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        // A user may only edit their own account
        if (userId != id)
        {
            logger.LogWarning("User {UserId} tried to edit user {TargetId}", userId, id);
            return Results.Forbid();
        }

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

        // The access token carries the username and email as claims, so a new one is issued;
        // otherwise the client would show the old values until the token expires.
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
}
