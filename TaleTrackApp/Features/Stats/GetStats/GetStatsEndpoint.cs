using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Stats.GetStats;

/// <summary>
/// <c>GET /api/stats</c>: the caller's yearly summary (media per type and per month, plus their
/// review count). Requires a valid JWT.
/// Builds the summary for the requested year, or the current one when none is given. Returns 401 if the token has no valid user id.
/// </summary>
public static class GetStatsEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, with request validation and the user policy.</summary>
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

    /// <summary>Get the caller's yearly summary</summary>
    private static async Task<IResult> HandleAsync(
        [AsParameters] GetStatsRequest request,
        StatsService statsService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var year = request.Year ?? DateTime.UtcNow.Year;
        var stats = await statsService.GetYearlyAsync(userId, year);
        return Results.Ok(new GetStatsResponse { Success = true, Data = stats });
    }
}
