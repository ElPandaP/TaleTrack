using System.Net;
using System.Text;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Malformed requests are turned away before they reach the business logic: a body that is not
/// JSON, a required field that is missing, or a value outside the ones the API accepts.
/// </summary>
[Collection(ApiCollection.Name)]
public class ValidationTests(CustomWebApplicationFactory factory)
{
    [Theory]
    [InlineData("POST", "/api/tracking/movies", "this is not json")]
    [InlineData("POST", "/api/tracking/movies", """{ "progress": 10 }""")]     // no title
    [InlineData("POST", "/api/reviews", """{ "rating": 7 }""")]                // no media
    [InlineData("GET", "/api/library?type=Comic", null)]                       // not a media type
    public async Task MalformedRequest_IsRejected(string method, string url, string? body)
    {
        var user = await factory.CreateUserAsync($"validation-{Guid.NewGuid():N}@test.com", $"v{Guid.NewGuid():N}"[..20]);
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (body != null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var res = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
