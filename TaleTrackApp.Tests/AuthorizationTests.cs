using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Every endpoint of the API needs a session except a fixed list (signing up, the steps of signing
/// in, the links sent by email and the profile picture). The tests read the endpoints from the
/// running app, so a new endpoint is checked as soon as it exists.
/// </summary>
[Collection(ApiCollection.Name)]
public class AuthorizationTests(CustomWebApplicationFactory factory)
{
    /// <summary>The only endpoints that work without a session.</summary>
    private static readonly HashSet<string> Anonymous =
    [
        "POST /api/register",
        "POST /api/login",
        "POST /api/auth/refresh",
        "POST /api/auth/logout",
        "POST /api/auth/request-code",
        "POST /api/auth/verify-code",
        "POST /api/auth/google",
        "POST /api/auth/google/complete",
        "POST /api/auth/request-password-reset",
        "POST /api/auth/reset-password",
        "POST /api/auth/confirm-delete",
        "POST /api/auth/revoke-signup",
        "GET /api/users/{id:guid}/avatar",
    ];

    /// <summary>
    /// Every API endpoint as "METHOD /route", with whether it allows anonymous access and whether
    /// it takes a form upload instead of JSON.
    /// </summary>
    private IEnumerable<(string Endpoint, bool AllowsAnonymous, bool TakesForm)> ApiEndpoints()
    {
        factory.CreateClient(); // makes sure the app has started
        var sources = factory.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();
        return from endpoint in sources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
               let route = "/" + endpoint.RoutePattern.RawText!.TrimStart('/')
               where route.StartsWith("/api/")
               from method in endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []
               let accepts = endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes ?? []
               select ($"{method} {route}", endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null,
                       accepts.Any(t => t.StartsWith("multipart/")));
    }

    [Fact]
    public void OnlyTheExpectedEndpoints_AllowAnonymousAccess()
    {
        var anonymous = ApiEndpoints().Where(e => e.AllowsAnonymous).Select(e => e.Endpoint).ToHashSet();
        Assert.Equal(Anonymous.Order(), anonymous.Order());
    }

    [Fact]
    public async Task EveryOtherEndpoint_RejectsARequestWithoutASession()
    {
        var client = factory.CreateClient();
        var problems = new List<string>();

        foreach (var (endpoint, allowsAnonymous, takesForm) in ApiEndpoints())
        {
            if (allowsAnonymous) continue;
            var parts = endpoint.Split(' ');
            var url = System.Text.RegularExpressions.Regex.Replace(parts[1], @"\{[^}]+\}", Guid.NewGuid().ToString());
            var request = new HttpRequestMessage(new HttpMethod(parts[0]), url);
            // The body only has to have the right content type: requests of another type are
            // turned away (415) before the session is even looked at.
            if (parts[0] is "POST" or "PUT")
                request.Content = takesForm ? new MultipartFormDataContent() : new StringContent("{}", Encoding.UTF8, "application/json");

            var res = await client.SendAsync(request);
            if (res.StatusCode != HttpStatusCode.Unauthorized)
                problems.Add($"{endpoint}: {(int)res.StatusCode}");
        }

        Assert.Empty(problems);
    }
}
