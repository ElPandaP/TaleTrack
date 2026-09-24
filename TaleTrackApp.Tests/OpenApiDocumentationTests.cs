using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// The published OpenAPI document must stay complete: a new endpoint or DTO that ships without
/// its summary, tag, typed responses or property comments fails here.
/// </summary>
[Collection(ApiCollection.Name)]
public class OpenApiDocumentationTests(CustomWebApplicationFactory factory)
{
    private static readonly string[] Methods = ["get", "post", "put", "delete", "patch"];

    // The default reason phrases Swashbuckle falls back to when nobody wrote a description.
    private static readonly string[] DefaultDescriptions =
        ["OK", "Bad Request", "Unauthorized", "Forbidden", "Not Found", "Internal Server Error", "Too Many Requests"];

    private async Task<JsonElement> DocumentAsync()
    {
        var client = factory.CreateClient();
        return await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
    }

    private static IEnumerable<(string Label, JsonElement Operation)> Operations(JsonElement doc) =>
        from path in doc.GetProperty("paths").EnumerateObject()
        from op in path.Value.EnumerateObject()
        where Methods.Contains(op.Name)
        select ($"{op.Name.ToUpperInvariant()} {path.Name}", op.Value);

    [Fact]
    public async Task EveryOperation_HasSummaryAndAKnownTag()
    {
        var doc = await DocumentAsync();
        var knownTags = doc.GetProperty("tags").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()).ToHashSet();

        var problems = new List<string>();
        foreach (var (label, op) in Operations(doc))
        {
            if (!op.TryGetProperty("summary", out var summary) || string.IsNullOrWhiteSpace(summary.GetString()))
                problems.Add($"{label}: no summary");

            var tags = op.TryGetProperty("tags", out var t) ? t.EnumerateArray().Select(x => x.GetString()).ToList() : [];
            if (tags.Count != 1 || !knownTags.Contains(tags[0]))
                problems.Add($"{label}: tag must be one of the documented tags, got [{string.Join(",", tags)}]");
        }

        Assert.Empty(problems);
    }

    [Fact]
    public async Task EveryOperation_DocumentsItsSuccessResponse()
    {
        var doc = await DocumentAsync();

        var problems = new List<string>();
        foreach (var (label, op) in Operations(doc))
        {
            var responses = op.GetProperty("responses");
            var success = responses.EnumerateObject().FirstOrDefault(r => r.Name.StartsWith('2'));
            if (success.Value.ValueKind != JsonValueKind.Object)
            {
                problems.Add($"{label}: no 2xx response");
                continue;
            }

            if (!success.Value.TryGetProperty("content", out _))
                problems.Add($"{label}: {success.Name} has no body schema");

            foreach (var response in responses.EnumerateObject())
            {
                var description = response.Value.TryGetProperty("description", out var d) ? d.GetString() : null;
                if (string.IsNullOrWhiteSpace(description) || DefaultDescriptions.Contains(description))
                    problems.Add($"{label}: {response.Name} has no description");
            }
        }

        Assert.Empty(problems);
    }

    [Fact]
    public async Task ProtectedOperations_RequireTheBearerScheme_AndAnonymousOnesDoNot()
    {
        var doc = await DocumentAsync();
        Assert.Equal("bearer", doc.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty("Bearer").GetProperty("scheme").GetString());

        var anonymous = new[]
        {
            "/api/register", "/api/login", "/api/auth/google", "/api/auth/google/complete",
            "/api/auth/request-code", "/api/auth/verify-code", "/api/auth/refresh", "/api/auth/logout",
            "/api/auth/request-password-reset", "/api/auth/reset-password", "/api/auth/confirm-delete",
            "/api/auth/revoke-signup", "/api/users/{id}/avatar",
        };

        var problems = new List<string>();
        foreach (var path in doc.GetProperty("paths").EnumerateObject())
        {
            foreach (var op in path.Value.EnumerateObject().Where(o => Methods.Contains(o.Name)))
            {
                var locked = op.Value.TryGetProperty("security", out var s) && s.GetArrayLength() > 0;
                if (locked == anonymous.Contains(path.Name))
                    problems.Add($"{op.Name.ToUpperInvariant()} {path.Name}: locked={locked}");
                if (locked && !op.Value.GetProperty("responses").TryGetProperty("401", out _))
                    problems.Add($"{op.Name.ToUpperInvariant()} {path.Name}: protected but no 401");
            }
        }

        Assert.Empty(problems);
    }

    [Fact]
    public async Task EverySchemaProperty_IsDescribed()
    {
        var doc = await DocumentAsync();

        var problems = new List<string>();
        foreach (var schema in doc.GetProperty("components").GetProperty("schemas").EnumerateObject())
        {
            if (!schema.Value.TryGetProperty("properties", out var properties)) continue;

            foreach (var property in properties.EnumerateObject())
            {
                // Nested objects and enums are described by their own schema.
                if (property.Value.TryGetProperty("$ref", out _) || property.Value.TryGetProperty("allOf", out _)) continue;
                if (!property.Value.TryGetProperty("description", out var d) || string.IsNullOrWhiteSpace(d.GetString()))
                    problems.Add($"{schema.Name}.{property.Name}");
            }
        }

        Assert.Empty(problems);
    }
}
