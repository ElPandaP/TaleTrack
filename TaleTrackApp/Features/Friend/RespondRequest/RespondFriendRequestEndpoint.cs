using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.Friend.RespondRequest;

public class RespondFriendRequestRequest
{
    [Required]
    public bool Accept { get; set; }
}

public static class RespondFriendRequestEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/friends/requests/{id:guid}", HandleAsync)
            .WithName("RespondFriendRequest")
            .WithDescription("Accept or decline an incoming friend request")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        RespondFriendRequestRequest request,
        FriendService friendService,
        ClaimsPrincipal user,
        ILogger<RespondFriendRequestRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        var result = await friendService.RespondAsync(userId, id, request.Accept);
        return result switch
        {
            RespondResult.Ok => Results.Ok(new { success = true }),
            RespondResult.NotFound => Results.NotFound(new { success = false, message = "Friend request not found." }),
            RespondResult.Forbidden => Results.Forbid(),
            _ => Results.StatusCode(500),
        };
    }
}
