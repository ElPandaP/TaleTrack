using MediaTrackerApp.Features.Media;
using MediaTrackerApp.Auth;

namespace MediaTrackerApp.Features.Media.GetMedia;

public static class GetMediaEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/media", HandleAsync)
            .WithName("GetMedia")
            .WithDescription("Obtiene la lista de medias con filtros (solo API interna)")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.InternalOnly);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] GetMediaRequest request,
        MediaService mediaService,
        ILogger<GetMediaRequest> logger)
    {
        try
        {
            var medias = await mediaService.GetWithFiltersAsync(
                request.Type,
                request.Limit,
                request.OrderBy
            );

            var response = new
            {
                success = true,
                count = medias.Count,
                data = medias.Select(m => new
                {
                    id = m.Id,
                    title = m.Title,
                    type = m.Type,
                    length = m.Length,
                    description = m.Description,
                    posterUrl = m.PosterUrl,
                    firstTrackedAt = m.FirstTrackedAt,
                    updatedAt = m.UpdatedAt
                })
            };

            logger.LogInformation($"GetMedia called with filters: Type={request.Type}, Limit={request.Limit}, OrderBy={request.OrderBy}");
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError($"Error retrieving media: {ex.Message}");
            return Results.StatusCode(500);
        }
    }
}
