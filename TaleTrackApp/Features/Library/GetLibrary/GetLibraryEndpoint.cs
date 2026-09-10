using System.Security.Claims;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.Library.GetLibrary;

public static class GetLibraryEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/library", HandleAsync)
            .WithName("GetLibrary")
            .WithDescription("The authenticated user's library (one row per media) with type/status/sort/year/limit filters")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] GetLibraryRequest request,
        LibraryService libraryService,
        ClaimsPrincipal user,
        ILogger<GetLibraryRequest> logger)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var all = await libraryService.GetForUserAsync(
                userId, request.Type, request.Status, request.Sort, request.Year);

            var data = request.Limit is int limit && limit > 0 ? all.Take(limit).ToList() : all;

            // `total` is the count before the limit — lets the client show "50 · see all".
            return Results.Ok(new { success = true, count = data.Count, total = all.Count, data });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving library for user");
            return Results.StatusCode(500);
        }
    }
}
