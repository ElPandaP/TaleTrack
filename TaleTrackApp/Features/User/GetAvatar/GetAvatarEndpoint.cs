using TaleTrackApp.OpenApi;
namespace TaleTrackApp.Features.User.GetAvatar;

/// <summary>
/// <c>GET /api/users/{id}/avatar</c>: serves a user's profile photo. Anonymous, because an
/// <c>img</c> tag cannot send an Authorization header.
/// Returns the WebP photo with a one-year cache header, or 404 if the user has no photo.
/// </summary>
public static class GetAvatarEndpoint
{
    /// <summary>Registers the route on the <c>/api</c> group as an anonymous endpoint.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/users/{id:guid}/avatar", HandleAsync)
            .WithName("GetAvatar")
            .WithTags("Users")
            .WithSummary("Get a user's profile photo")
            .WithDescription("Anonymous because an `img` tag cannot send an Authorization header. Cached for a year; the `avatarUrl` of a user changes whenever the photo does.")
            .Produces(StatusCodes.Status200OK, typeof(byte[]), "image/webp")
            .WithMetadata(new ResponseDescription(StatusCodes.Status200OK, "The photo, a 256x256 WebP image."))
            .Responds(StatusCodes.Status404NotFound, "The user has no photo.")
            .AllowAnonymous();
    }

    /// <summary>Get a user's profile photo</summary>
    private static async Task<IResult> HandleAsync(
        Guid id,
        AvatarService avatarService,
        HttpContext http)
    {
        var avatar = await avatarService.GetAsync(id);
        if (avatar is null) return Results.NotFound();

        // The URL is cache-busted with ?v=<ticks> whenever the photo changes.
        http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return Results.File(avatar.Data, avatar.ContentType);
    }
}
