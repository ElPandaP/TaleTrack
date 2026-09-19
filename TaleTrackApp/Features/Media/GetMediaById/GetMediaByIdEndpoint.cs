using System.Security.Claims;
using TaleTrackApp.Security;

namespace TaleTrackApp.Features.Media.GetMediaById;

public static class GetMediaByIdEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/media/{id:guid}", HandleAsync)
            .WithName("GetMediaById")
            .WithDescription("A media's detail page: data, the user's progress and review, and every review")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        MediaService mediaService,
        ClaimsPrincipal user,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(GetMediaByIdEndpoint));

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            return Results.Unauthorized();

        try
        {
            var detail = await mediaService.GetDetailAsync(id, userId);
            if (detail == null)
                return Results.NotFound(new { success = false, message = "Media not found" });

            return Results.Ok(GetMediaByIdResponse.From(detail, userId));
        }
        catch (Exception ex)
        {
            logger.LogError("Error retrieving media {Id}: {Message}", id, ex.Message);
            return Results.StatusCode(500);
        }
    }
}
