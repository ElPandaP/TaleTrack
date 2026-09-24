using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.User.DeleteAvatar;

public static class DeleteAvatarEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/users/me/avatar", HandleAsync)
            .WithName("DeleteAvatar")
            .WithTags("Users")
            .WithSummary("Remove the caller's profile photo")
            .Responds<ApiResult>("Photo removed (also when there was none).")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        AvatarService avatarService,
        ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        await avatarService.RemoveAsync(userId);
        return Results.Ok(new { success = true });
    }
}
