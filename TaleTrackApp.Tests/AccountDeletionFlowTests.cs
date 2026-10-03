using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TaleTrackApp.Model;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Deleting an account through an emailed link: asking for it only sends the link, and the link
/// deletes the account together with everything that belongs to it. A link that was already used,
/// has expired, was tampered with or was issued for something else deletes nothing.
/// </summary>
[Collection(ApiCollection.Name)]
public class AccountDeletionFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    /// <summary>Asks to delete the user's account and returns the token of the emailed link.</summary>
    private async Task<string> DeleteLinkTokenAsync(TestUser user, string email)
    {
        var res = await user.Client.PostAsJsonAsync("/api/auth/request-account-deletion", new { Locale = "en" });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await _factory.Resend.WaitForAsync(email, "Confirm your account deletion")).LinkToken!;
    }

    private Task<HttpResponseMessage> ConfirmAsync(string token) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/confirm-delete", new { Token = token });

    private async Task<bool> UserExistsAsync(Guid userId)
    {
        using var scope = _factory.NewDbScope(out var db);
        return await db.Users.AnyAsync(u => u.Id == userId);
    }

    [Fact]
    public async Task RequestDeletion_EmailsTheLink_AndKeepsTheAccount()
    {
        var user = await _factory.CreateUserAsync("delete-request@test.com", "deleterequest");

        await DeleteLinkTokenAsync(user, "delete-request@test.com");
        Assert.True(await UserExistsAsync(user.Id));
    }

    [Fact]
    public async Task ConfirmDelete_RemovesTheAccountAndEverythingItOwns()
    {
        var user = await _factory.CreateUserAsync("delete-confirm@test.com", "deleteconfirm");
        var friend = await _factory.CreateUserAsync("delete-confirm-friend@test.com", "deleteconfirmfriend");
        var mediaId = await user.Client.TrackMovieAsync("Deleted Account Movie", 100);
        await user.Client.AddReviewAsync(mediaId, 7);
        await ApiHelpers.BefriendAsync(user, friend);

        var confirm = await ConfirmAsync(await DeleteLinkTokenAsync(user, "delete-confirm@test.com"));
        Assert.True(confirm.IsSuccessStatusCode, await confirm.Content.ReadAsStringAsync());

        using var scope = _factory.NewDbScope(out var db);
        Assert.False(await db.Users.AnyAsync(u => u.Id == user.Id));
        Assert.False(await db.TrackingEvents.AnyAsync(t => t.UserId == user.Id));
        Assert.False(await db.Reviews.AnyAsync(r => r.UserId == user.Id));
        Assert.False(await db.Friendships.AnyAsync(f => f.RequesterId == user.Id || f.AddresseeId == user.Id));
        Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == user.Id));
        // The film itself stays: other users may track it too.
        Assert.True(await db.Medias.AnyAsync(m => m.Id == mediaId));
    }

    [Theory]
    [InlineData("used")]
    [InlineData("expired")]
    [InlineData("tampered")]
    [InlineData("other purpose")]
    public async Task ConfirmDelete_WithAnInvalidLink_DeletesNothing(string problem)
    {
        var email = $"delete-badlink-{problem.Replace(' ', '-')}@test.com";
        var user = await _factory.CreateUserAsync(email, $"deletebad{problem.Replace(" ", "")}");
        var token = await DeleteLinkTokenAsync(user, email);

        switch (problem)
        {
            case "used":
                // A first use would already delete the account, so the token is marked consumed
                // directly, as that first use would have left it.
                await _factory.UpdateActionTokenAsync(token, t => t.ConsumedAt = DateTime.UtcNow);
                break;
            case "expired":
                await _factory.UpdateActionTokenAsync(token, t => t.ExpiresAt = DateTime.UtcNow.AddMinutes(-1));
                break;
            case "tampered":
                token += "x";
                break;
            case "other purpose":
                token = await _factory.IssueActionTokenAsync(user.Id, AuthActionToken.PasswordReset);
                break;
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await ConfirmAsync(token)).StatusCode);
        Assert.True(await UserExistsAsync(user.Id));
    }
}
