using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TaleTrackApp.Features.Auth;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// POST /api/auth/google/complete: the second half of a Google sign-up. The pending token is
/// issued through the app's own service, standing in for the id_token check that needs a real
/// Google response.
/// </summary>
[Collection(ApiCollection.Name)]
public class GoogleSignupCompleteFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private string PendingToken(string googleId, string email)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<GoogleSignupTokenService>()
            .Issue(googleId, email, "Test Name");
    }

    private static async Task<string> MyUsernameAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var res = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").GetProperty("username").GetString()!;
    }

    [Fact]
    public async Task CompleteSignup_CreatesTheAccountAndReturnsASession()
    {
        var client = _factory.CreateClient();
        var pending = PendingToken("google-new-1", "gnew1@test.com");

        var res = await client.PostAsJsonAsync("/api/auth/google/complete",
            new { PendingToken = pending, Username = "gnewuser1" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.Equal("gnewuser1", await MyUsernameAsync(client, body.GetProperty("token").GetString()!));
    }

    [Fact]
    public async Task CompleteSignup_Repeated_LogsIntoTheAccountAlreadyCreated()
    {
        var client = _factory.CreateClient();
        var pending = PendingToken("google-new-2", "gnew2@test.com");

        await client.PostAsJsonAsync("/api/auth/google/complete",
            new { PendingToken = pending, Username = "gnewuser2" });

        // A double submit, even with another username, must not create a second account.
        var again = await client.PostAsJsonAsync("/api/auth/google/complete",
            new { PendingToken = pending, Username = "gnewuser2b" });

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var body = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("gnewuser2", await MyUsernameAsync(client, body.GetProperty("token").GetString()!));
    }

    [Fact]
    public async Task CompleteSignup_UsernameTaken_Returns400()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/register",
            new { Email = "gtaken@test.com", Username = "gtakenuser", Password = "Password1!" });
        var pending = PendingToken("google-new-3", "gnew3@test.com");

        var res = await client.PostAsJsonAsync("/api/auth/google/complete",
            new { PendingToken = pending, Username = "GTakenUser" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("username_taken", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CompleteSignup_InvalidToken_Returns400()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/auth/google/complete",
            new { PendingToken = "not-a-token", Username = "whoever" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_or_expired", body.GetProperty("code").GetString());
    }
}
