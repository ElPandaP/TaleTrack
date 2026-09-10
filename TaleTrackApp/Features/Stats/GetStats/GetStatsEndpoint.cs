using System.Security.Claims;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.Stats.GetStats;

public static class GetStatsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/stats", HandleAsync)
            .WithName("GetStats")
            .WithDescription("The authenticated user's consumption summary for a year (defaults to the current one)")
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
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var year = request.Year ?? DateTime.UtcNow.Year;
            var stats = await statsService.GetYearlyAsync(userId, year);
            return Results.Ok(new { success = true, data = stats });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving stats for user");
            return Results.StatusCode(500);
        }
    }
}
