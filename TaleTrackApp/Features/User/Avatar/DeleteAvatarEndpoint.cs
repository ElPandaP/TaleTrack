using System.Security.Claims;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.User.Avatar;

public static class DeleteAvatarEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/user/avatar", HandleAsync)
            .WithName("DeleteAvatar")
            .WithDescription("Removes the authenticated user's profile photo (requires JWT)")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        AvatarService avatarService,
        ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Results.Unauthorized();

        await avatarService.RemoveAsync(userId);
        return Results.Ok(new { success = true });
    }
}
