using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>Shortcuts for the API calls that many tests need to set up their scenario.</summary>
public static class ApiHelpers
{
    /// <summary>The caller's library, with an optional query string (for example <c>?type=Movie</c>).</summary>
    public static async Task<JsonElement> LibraryAsync(this HttpClient client, string query = "")
    {
        var res = await client.GetAsync($"/api/library{query}");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>The id of the media titled <paramref name="title"/> in the caller's library.</summary>
    public static async Task<Guid> MediaIdAsync(this HttpClient client, string title)
    {
        var library = await client.LibraryAsync();
        return library.GetProperty("data").EnumerateArray()
            .First(i => i.GetProperty("titleEN").GetString() == title || i.GetProperty("titleES").GetString() == title)
            .GetProperty("mediaId").GetGuid();
    }

    /// <summary>Tracks a movie and returns its media id.</summary>
    public static async Task<Guid> TrackMovieAsync(this HttpClient client, string title, int progress, string? language = null)
    {
        var res = await client.PostAsJsonAsync("/api/tracking/movies",
            new { Title = title, Minutes = 100, Progress = progress, Language = language });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return await client.MediaIdAsync(title);
    }

    /// <summary>The detail of a media as the caller sees it (<c>GET /api/media/{id}</c>).</summary>
    public static async Task<JsonElement> MediaDetailAsync(this HttpClient client, Guid mediaId)
    {
        var res = await client.GetAsync($"/api/media/{mediaId}");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    /// <summary>Reviews a media and returns the review id.</summary>
    public static async Task<Guid> AddReviewAsync(this HttpClient client, Guid mediaId, int rating, string? comment = null)
    {
        var res = await client.PostAsJsonAsync("/api/reviews", new { MediaId = mediaId, Rating = rating, Comment = comment });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid();
    }

    /// <summary>Makes <paramref name="a"/> and <paramref name="b"/> friends: a sends the request and b accepts it.</summary>
    public static async Task BefriendAsync(TestUser a, TestUser b)
    {
        var sent = await a.Client.PostAsJsonAsync("/api/friends/requests", new { UserId = b.Id });
        Assert.True(sent.IsSuccessStatusCode, await sent.Content.ReadAsStringAsync());
        var accepted = await b.Client.PutAsync($"/api/friends/requests/{await IncomingRequestIdAsync(b.Client, a.Id)}", null);
        Assert.True(accepted.IsSuccessStatusCode, await accepted.Content.ReadAsStringAsync());
    }

    /// <summary>The id of the pending request that <paramref name="fromUserId"/> sent to the caller.</summary>
    public static async Task<Guid> IncomingRequestIdAsync(HttpClient client, Guid fromUserId)
    {
        var friends = await client.GetFromJsonAsync<JsonElement>("/api/friends");
        return friends.GetProperty("incoming").EnumerateArray()
            .First(r => r.GetProperty("userId").GetGuid() == fromUserId)
            .GetProperty("requestId").GetGuid();
    }

    /// <summary>The caller's active sessions (the "Conexiones" page).</summary>
    public static async Task<JsonElement> SessionsAsync(this HttpClient client)
    {
        var res = await client.GetAsync("/api/auth/sessions");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    /// <summary>The <c>code</c> field of an error response.</summary>
    public static async Task<string?> CodeAsync(this HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

    /// <summary>Asks to renew a session with a refresh token.</summary>
    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
}
