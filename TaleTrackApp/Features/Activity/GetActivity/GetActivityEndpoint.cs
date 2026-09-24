using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Features.Activity;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Activity.GetActivity;

public static class GetActivityEndpoint
{
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

    private static async Task<IResult> HandleAsync(
        [AsParameters] GetActivityRequest request,
        ActivityService activityService,
        ClaimsPrincipal user,
        ILogger<GetActivityRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        try
        {
            var limit = request.Limit ?? 200;
            var feed = request.UserId is Guid target
                ? await activityService.GetForUserAsync(userId, target, limit)
                : await activityService.GetFeedAsync(userId, request.Scope ?? "all", limit);

            return Results.Ok(new GetActivityResponse { Success = true, Count = feed.Count, Data = feed });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error building activity feed");
            return Results.StatusCode(500);
        }
    }
}
