using System.Security.Claims;
using TaleTrackApp.Features.TrackingEvent;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.TrackingEvent.GetTrackingEvents;

public static class GetTrackingEventsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/tracking", HandleAsync)
            .WithName("GetTrackingEvents")
            .WithDescription("Obtiene los eventos de tracking del usuario autenticado (requiere JWT + API Key interna)")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy)
            .RequireAuthorization(Policies.InternalOnly);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] GetTrackingEventsRequest request,
        TrackingEventService trackingEventService,
        ClaimsPrincipal user,
        ILogger<GetTrackingEventsRequest> logger)
    {
        // Obtener el UserId del JWT
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT token");
            return Results.Unauthorized();
        }

        try
        {
            var trackingEvents = await trackingEventService.GetByUserIdWithFiltersAsync(
                userId,
                request.Type,
                request.Limit,
                request.OrderBy
            );

            var response = new
            {
                success = true,
                count = trackingEvents.Count,
                data = trackingEvents.Select(te => new
                {
                    id = te.Id,
                    userId = te.UserId,
                    mediaId = te.MediaId,
                    progress = te.Progress,
                    eventDate = te.EventDate,
                    media = new
                    {
                        id = te.Media?.Id,
                        title = te.Media?.Title,
                        type = te.Media?.Type,
                        length = te.Media?.Length,
                        description = te.Media?.Description,
                        posterUrl = te.Media?.PosterUrl,
                        firstTrackedAt = te.Media?.FirstTrackedAt,
                        updatedAt = te.Media?.UpdatedAt
                    }
                })
            };

            logger.LogInformation($"GetTrackingEvents called for user {userId} with filters: Type={request.Type}, Limit={request.Limit}, OrderBy={request.OrderBy}");
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError($"Error retrieving tracking events: {ex.Message}");
            return Results.StatusCode(500);
        }
    }
}
