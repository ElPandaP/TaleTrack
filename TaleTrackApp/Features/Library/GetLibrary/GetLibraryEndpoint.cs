using TaleTrackApp.OpenApi;
using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Library.GetLibrary;

/// <summary>
/// <c>GET /api/library</c>: the caller's library, one row per tracked media, with optional
/// filters, sorting and limit. Requires a valid JWT.
/// Loads the filtered and sorted library and applies the limit. Returns 401 if the token has no valid user id.
/// </summary>
public static class GetLibraryEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group, with request validation and the user policy.</summary>
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

    /// <summary>List the caller's library</summary>
    private static async Task<IResult> HandleAsync(
        [AsParameters] GetLibraryRequest request,
        LibraryService libraryService,
        ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();

        var all = await libraryService.GetForUserAsync(
            userId, request.Type, request.Status, request.Sort, request.Year);

        var data = request.Limit is int limit && limit > 0 ? all.Take(limit).ToList() : all;

        // `total` is the count before the limit, so a client can show "50 of N, see all".
        return Results.Ok(new GetLibraryResponse { Success = true, Count = data.Count, Total = all.Count, Data = data });
    }
}
