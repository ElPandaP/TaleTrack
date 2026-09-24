using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Stats.GetStats;

public static class GetStatsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/stats", HandleAsync)
            .WithName("GetStats")
            .WithTags("Stats")
            .WithSummary("Get the caller's yearly summary")
            .WithDescription("Counts distinct media with tracking activity in the year, bucketed by the month of their latest activity. Defaults to the current year.")
            .Responds<GetStatsResponse>("The yearly summary.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] GetStatsRequest request,
        StatsService statsService,
        ClaimsPrincipal user,
        ILogger<GetStatsRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var year = request.Year ?? DateTime.UtcNow.Year;
            var stats = await statsService.GetYearlyAsync(userId, year);
            return Results.Ok(new GetStatsResponse { Success = true, Data = stats });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving stats for user");
            return Results.StatusCode(500);
        }
    }
}
