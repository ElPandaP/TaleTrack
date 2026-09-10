namespace TaleTrackApp.Features.User.Avatar;

public static class GetAvatarEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/users/{id:int}/avatar", HandleAsync)
            .WithName("GetAvatar")
            .WithDescription("Serves a user's profile photo (public — an <img> tag can't send a token)")
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        int id,
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
