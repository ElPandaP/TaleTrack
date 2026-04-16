using TaleTrackApp.Features.Media;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.Media.AddMedia;

public static class AddMediaEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/media", HandleAsync)
            .WithName("AddMedia")
            .WithDescription("Agrega un nuevo media item")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(Policies.UserPolicy); // Requiere usuario autenticado
    }

    private static async Task<IResult> HandleAsync(AddMediaRequest request, MediaService mediaService, ILogger<AddMediaRequest> logger)
    {
        // Process
        try
        {
            var media = await mediaService.CreateAsync(request.Title, request.Type, request.Length);

            logger.LogInformation($"Media '{media.Title}' added successfully");
            return Results.Ok(new { success = true, message = "Media agregado exitosamente" });
        }
        catch (Exception ex)
        {
            logger.LogError($"Error adding media: {ex.Message}");
            return Results.StatusCode(500);
        }
    }
}
