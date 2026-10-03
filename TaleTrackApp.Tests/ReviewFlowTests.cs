using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Reviews: only titles in the user's library can be reviewed, with a rating from 1 to 10 and at
/// most one review per title; editing and deleting are for the author only; and reviewing a
/// finished title takes it off the "pending reviews" list, while deleting the review puts it back.
/// </summary>
[Collection(ApiCollection.Name)]
public class ReviewFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static async Task<bool> IsPendingAsync(HttpClient client, Guid mediaId)
    {
        var pending = await client.GetFromJsonAsync<JsonElement>("/api/reviews/pending");
        return pending.GetProperty("data").EnumerateArray().Any(i => i.GetProperty("mediaId").GetGuid() == mediaId);
    }

    private static async Task<JsonElement> MyReviewsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/reviews")).GetProperty("data");

    [Theory]
    [InlineData(1, null)]       // lowest rating, no comment
    [InlineData(10, "Loved it")] // highest rating, with a comment
    public async Task AddReview_ShowsUpEverywhere_AndLeavesThePendingList(int rating, string? comment)
    {
        var user = await _factory.CreateUserAsync($"review-add-{rating}@test.com", $"reviewadd{rating}");
        var mediaId = await user.Client.TrackMovieAsync($"Reviewed Film {rating}", 100);
        Assert.True(await IsPendingAsync(user.Client, mediaId));

        await user.Client.AddReviewAsync(mediaId, rating, comment);

        var mine = (await MyReviewsAsync(user.Client))[0];
        Assert.Equal(rating, mine.GetProperty("rating").GetInt32());
        Assert.Equal(comment, mine.GetProperty("comment").GetString());
        Assert.Equal(rating, (await user.Client.MediaDetailAsync(mediaId)).GetProperty("myRating").GetInt32());
        Assert.False(await IsPendingAsync(user.Client, mediaId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task AddReview_RatingOutsideOneToTen_IsRejected(int rating)
    {
        var user = await _factory.CreateUserAsync($"review-range-{rating}@test.com", $"reviewrange{rating}");
        var mediaId = await user.Client.TrackMovieAsync("Wandering Ember", 100);

        var res = await user.Client.PostAsJsonAsync("/api/reviews", new { MediaId = mediaId, Rating = rating });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Theory]
    [InlineData("not tracked")]
    [InlineData("does not exist")]
    public async Task AddReview_OfATitleNotInTheLibrary_IsRejected(string problem)
    {
        var user = await _factory.CreateUserAsync($"review-notmine-{problem.Replace(' ', '-')}@test.com", $"reviewnotmine{problem.Replace(" ", "")}");
        var mediaId = Guid.NewGuid();
        if (problem == "not tracked")
        {
            var owner = await _factory.CreateUserAsync("review-notmine-owner@test.com", "reviewnotmineowner");
            mediaId = await owner.Client.TrackMovieAsync("Someone Elses Film", 100);
        }

        var res = await user.Client.PostAsJsonAsync("/api/reviews", new { MediaId = mediaId, Rating = 7 });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(0, (await MyReviewsAsync(user.Client)).GetArrayLength());
    }

    [Fact]
    public async Task AddReview_Twice_KeepsASingleReview()
    {
        var user = await _factory.CreateUserAsync("review-twice@test.com", "reviewtwice");
        var mediaId = await user.Client.TrackMovieAsync("Twice Reviewed Film", 100);

        await user.Client.AddReviewAsync(mediaId, 4);
        await user.Client.AddReviewAsync(mediaId, 9);

        var mine = await MyReviewsAsync(user.Client);
        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal(9, mine[0].GetProperty("rating").GetInt32());
    }

    [Theory]
    [InlineData("Actually great")]
    [InlineData(null)] // sending no comment clears it
    public async Task EditReview_ReplacesRatingAndComment(string? newComment)
    {
        var user = await _factory.CreateUserAsync($"review-edit-{newComment is null}@test.com", $"reviewedit{newComment is null}");
        var reviewId = await user.Client.AddReviewAsync(await user.Client.TrackMovieAsync("Copper Skyline", 100), 5, "First take");

        var res = await user.Client.PutAsJsonAsync($"/api/reviews/{reviewId}", new { Rating = 9, Comment = newComment });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        var mine = (await MyReviewsAsync(user.Client))[0];
        Assert.Equal(9, mine.GetProperty("rating").GetInt32());
        Assert.Equal(newComment, mine.GetProperty("comment").GetString());
    }

    [Fact]
    public async Task DeleteReview_RemovesIt_AndTheTitleIsPendingAgain()
    {
        var user = await _factory.CreateUserAsync("review-delete@test.com", "reviewdelete");
        var mediaId = await user.Client.TrackMovieAsync("Faded Marquee", 100);
        var reviewId = await user.Client.AddReviewAsync(mediaId, 6);

        var res = await user.Client.DeleteAsync($"/api/reviews/{reviewId}");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        Assert.Equal(0, (await MyReviewsAsync(user.Client)).GetArrayLength());
        Assert.True(await IsPendingAsync(user.Client, mediaId));
    }

    [Theory]
    [InlineData("edit")]
    [InlineData("delete")]
    public async Task ChangingSomeoneElsesReview_IsDenied(string action)
    {
        var owner = await _factory.CreateUserAsync($"review-owner-{action}@test.com", $"reviewowner{action}");
        var stranger = await _factory.CreateUserAsync($"review-stranger-{action}@test.com", $"reviewstranger{action}");
        var reviewId = await owner.Client.AddReviewAsync(await owner.Client.TrackMovieAsync("Hollow Lantern", 100), 7);

        var res = action == "edit"
            ? await stranger.Client.PutAsJsonAsync($"/api/reviews/{reviewId}", new { Rating = 1 })
            : await stranger.Client.DeleteAsync($"/api/reviews/{reviewId}");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal(7, (await MyReviewsAsync(owner.Client))[0].GetProperty("rating").GetInt32());
    }
}
