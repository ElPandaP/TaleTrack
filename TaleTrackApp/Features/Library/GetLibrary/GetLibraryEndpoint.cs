using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Library.GetLibrary;

public static class GetLibraryEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/library", HandleAsync)
            .WithName("GetLibrary")
            .WithTags("Library")
            .WithSummary("List the caller's library")
            .WithDescription("Everything the caller tracks, one row per media, with their progress and rating. `total` counts the matches before `limit` is applied, `count` what was returned.")
            .Responds<GetLibraryResponse>("The matching library rows.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
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
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
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
            return Results.Ok(new GetLibraryResponse { Success = true, Count = data.Count, Total = all.Count, Data = data });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving library for user");
            return Results.StatusCode(500);
        }
    }
}
