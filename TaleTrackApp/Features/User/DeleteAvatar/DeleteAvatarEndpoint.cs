using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.User.DeleteAvatar;

/// <summary>
/// <c>DELETE /api/users/me/avatar</c>: removes the caller's profile photo. Requires a valid JWT.
/// Removes the photo; succeeds also when there was none. Returns 401 if the token has no valid user id.
/// </summary>
public static class DeleteAvatarEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, behind the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/users/me/avatar", HandleAsync)
            .WithName("DeleteAvatar")
            .WithTags("Users")
            .WithSummary("Remove the caller's profile photo")
            .Responds<ApiResult>("Photo removed (also when there was none).")
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Remove the caller's profile photo</summary>
    private static async Task<IResult> HandleAsync(
        AvatarService avatarService,
        ClaimsPrincipal principal)
    {
        if (!principal.TryGetUserId(out var userId))
            return Results.Unauthorized();

        await avatarService.RemoveAsync(userId);
        return Results.Ok(new { success = true });
    }
}
