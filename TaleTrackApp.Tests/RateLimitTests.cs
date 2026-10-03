using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// The per-IP rate limit. The rest of the suite runs without it, so this test starts its own copy
/// of the app with a limit of five requests per minute and tells requests apart by the client IP
/// that the reverse proxy would forward.
/// </summary>
[Collection(ApiCollection.Name)]
public class RateLimitTests(CustomWebApplicationFactory factory)
{
    private const int Limit = 5;

    private static HttpRequestMessage LoginFrom(string ip)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/login")
        {
            Content = JsonContent.Create(new { Email = "rate-limit@test.com", Password = "Whatever1" })
        };
        request.Headers.Add("X-Forwarded-For", ip);
        return request;
    }

    [Fact]
    public async Task RequestsOverTheLimit_AreRejected_OnlyForThatIp()
    {
        using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:PermitLimit", $"{Limit}"));
        var client = limited.CreateClient();

        for (var i = 0; i < Limit; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(LoginFrom("203.0.113.1"))).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(LoginFrom("203.0.113.1"))).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(LoginFrom("203.0.113.2"))).StatusCode);
    }
}
