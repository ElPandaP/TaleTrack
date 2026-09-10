using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Add / edit / delete a review, ownership enforcement, and how reviewing a
/// finished title clears it from the "pending reviews" list.
/// </summary>
[Collection(ApiCollection.Name)]
public class ReviewFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private async Task<HttpClient> AuthedClientAsync(string email, string username)
    {
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/register",
            new { Email = email, Username = username, Password = "Password1!" });
        var login = await client.PostAsJsonAsync("/api/login",
            new { Email = email, Password = "Password1!" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return client;
    }

    /// <summary>Tracks a finished movie and returns its mediaId, so tests have something to review.</summary>
    private static async Task<int> TrackFinishedMovieAsync(HttpClient client, string title)
    {
        await client.PostAsJsonAsync("/api/tracking/movies", new { Title = title, Minutes = 100, Progress = 100 });
        var lib = await client.GetAsync("/api/library?type=Movie");
        var body = await lib.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").EnumerateArray()
            .First(i => i.GetProperty("title").GetString() == title)
            .GetProperty("mediaId").GetInt32();
    }

    private static async Task<int> AddReviewAsync(HttpClient client, int mediaId, int rating, string? comment = null)
    {
        var res = await client.PostAsJsonAsync("/api/review", new { MediaId = mediaId, Rating = rating, Comment = comment });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task AddReview_AppearsInMyReviews_AndClearsFromPending()
    {
        var client = await AuthedClientAsync("review-add@test.com", "reviewadd");
        var mediaId = await TrackFinishedMovieAsync(client, "The Silent Orbit");

        var pendingBefore = await client.GetAsync("/api/reviews/pending");
        var pendingBeforeBody = await pendingBefore.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(pendingBeforeBody.GetProperty("data").EnumerateArray(),
            i => i.GetProperty("mediaId").GetInt32() == mediaId);

        await AddReviewAsync(client, mediaId, 8, "Loved it");

        var mine = await client.GetAsync("/api/reviews");
        var mineBody = await mine.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, mineBody.GetProperty("count").GetInt32());
        Assert.Equal(8, mineBody.GetProperty("data")[0].GetProperty("rating").GetInt32());

        var pendingAfter = await client.GetAsync("/api/reviews/pending");
        var pendingAfterBody = await pendingAfter.Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(pendingAfterBody.GetProperty("data").EnumerateArray(),
            i => i.GetProperty("mediaId").GetInt32() == mediaId);
    }

    [Fact]
    public async Task EditReview_UpdatesRatingAndComment()
    {
        var client = await AuthedClientAsync("review-edit@test.com", "reviewedit");
        var mediaId = await TrackFinishedMovieAsync(client, "Copper Skyline");
        var reviewId = await AddReviewAsync(client, mediaId, 5);

        var editRes = await client.PutAsJsonAsync($"/api/review/{reviewId}", new { Rating = 9, Comment = "Actually great" });
        Assert.True(editRes.IsSuccessStatusCode, await editRes.Content.ReadAsStringAsync());

        var mine = await client.GetAsync("/api/reviews");
        var mineBody = await mine.Content.ReadFromJsonAsync<JsonElement>();
        var item = mineBody.GetProperty("data")[0];
        Assert.Equal(9, item.GetProperty("rating").GetInt32());
        Assert.Equal("Actually great", item.GetProperty("comment").GetString());
    }

    [Fact]
    public async Task DeleteReview_RemovesIt()
    {
        var client = await AuthedClientAsync("review-delete@test.com", "reviewdelete");
        var mediaId = await TrackFinishedMovieAsync(client, "Faded Marquee");
        var reviewId = await AddReviewAsync(client, mediaId, 6);

        var deleteRes = await client.DeleteAsync($"/api/review/{reviewId}");
        Assert.True(deleteRes.IsSuccessStatusCode, await deleteRes.Content.ReadAsStringAsync());

        var mine = await client.GetAsync("/api/reviews");
        var mineBody = await mine.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, mineBody.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task EditReview_ByNonOwner_Returns403()
    {
        var owner = await AuthedClientAsync("review-owner-edit@test.com", "reviewowneredit");
        var mediaId = await TrackFinishedMovieAsync(owner, "Hollow Lantern");
        var reviewId = await AddReviewAsync(owner, mediaId, 7);

        var stranger = await AuthedClientAsync("review-stranger-edit@test.com", "reviewstrangeredit");
        var editRes = await stranger.PutAsJsonAsync($"/api/review/{reviewId}", new { Rating = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, editRes.StatusCode);
    }

    [Fact]
    public async Task DeleteReview_ByNonOwner_Returns403()
    {
        var owner = await AuthedClientAsync("review-owner-del@test.com", "reviewownerdel");
        var mediaId = await TrackFinishedMovieAsync(owner, "Brittle Horizon");
        var reviewId = await AddReviewAsync(owner, mediaId, 4);

        var stranger = await AuthedClientAsync("review-stranger-del@test.com", "reviewstrangerdel");
        var deleteRes = await stranger.DeleteAsync($"/api/review/{reviewId}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteRes.StatusCode);
    }

    [Fact]
    public async Task AddReview_RatingOutOfRange_Returns400()
    {
        var client = await AuthedClientAsync("review-invalid@test.com", "reviewinvalid");
        var mediaId = await TrackFinishedMovieAsync(client, "Wandering Ember");

        var res = await client.PostAsJsonAsync("/api/review", new { MediaId = mediaId, Rating = 99 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
