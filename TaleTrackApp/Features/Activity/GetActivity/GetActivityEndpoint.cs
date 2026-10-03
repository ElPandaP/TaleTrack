using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Activity;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Activity.GetActivity;

/// <summary>
/// <c>GET /api/activity</c>: the activity feed (what people started, finished and reviewed), or a
/// single user's activity for their public profile. Requires a valid JWT.
/// Returns the feed for the caller. With <c>userId</c> it returns only that user's activity (empty unless the caller is that user or a friend); otherwise <c>scope</c> picks whose activity to include. Returns 401 if the token has no valid user id.
/// </summary>
public static class GetActivityEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, with request validation and the user policy.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/activity", HandleAsync)
            .WithName("GetActivity")
            .WithTags("Activity")
            .WithSummary("Get the activity feed")
            .WithDescription("What people started, finished and reviewed, newest first. Friends' entries respect their feed-privacy settings. `scope` picks whose activity to show (default `all`, the caller and their friends); `userId` overrides it to show a single user, as on a public profile: the result is empty unless the caller is that user or one of their friends.")
            .Responds<GetActivityResponse>("The feed.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    /// <summary>Get the activity feed</summary>
    private static async Task<IResult> HandleAsync(
        [AsParameters] GetActivityRequest request,
        ActivityService activityService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var limit = request.Limit ?? 200;
        var feed = request.UserId is Guid target
            ? await activityService.GetForUserAsync(userId, target, limit)
            : await activityService.GetFeedAsync(userId, request.Scope ?? "all", limit);

        return Results.Ok(new GetActivityResponse { Success = true, Count = feed.Count, Data = feed });
    }
}
