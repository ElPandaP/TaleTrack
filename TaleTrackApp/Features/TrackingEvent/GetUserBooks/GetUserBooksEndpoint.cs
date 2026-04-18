using System.Security.Claims;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.TrackingEvent.GetUserBooks;

public static class GetUserBooksEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/books", HandleAsync)
            .WithName("GetUserBooks")
            .WithDescription("Obtiene los libros leídos del usuario autenticado")
            .RequireAuthorization(Policies.UserPolicy);
    }

    private static async Task<IResult> HandleAsync(
        TrackingEventService trackingEventService,
        ClaimsPrincipal user,
        ILoggerFactory loggerFactory)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Results.Unauthorized();

        var logger = loggerFactory.CreateLogger(nameof(GetUserBooksEndpoint));
        try
        {
            var events = await trackingEventService.GetByUserIdWithFiltersAsync(
                userId, mediaType: "Book", orderBy: "date_desc");

            // Deduplicate: keep only the most recent tracking event per book
            var books = events
                .GroupBy(te => te.MediaId)
                .Select(g => g.First())
                .Select(te => new
                {
                    id         = te.Media!.Id,
                    title      = te.Media.Title,
                    author     = te.Media.Author,
                    coverUrl   = te.Media.PosterUrl,
                    isbn       = te.Media.Isbn,
                    pages      = te.Media.Length,
                    finishedAt = te.EventDate,
                })
                .ToList();

            return Results.Ok(new { success = true, count = books.Count, data = books });
        }
        catch (Exception ex)
        {
            logger.LogError("Error retrieving user books: {Message}", ex.Message);
            return Results.StatusCode(500);
        }
    }
}
