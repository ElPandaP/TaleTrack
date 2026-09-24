using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TaleTrackApp.OpenApi;

public static class OpenApiConfiguration
{
    public const string BearerScheme = "Bearer";

    private const string Description = """
        REST API behind the TaleTrack web app, the Netflix browser extension and the KOReader plugin.
        It is public by design: third parties can build their own clients.

        **Authentication.** JWT bearer. Get a token from `POST /api/login`, `POST /api/auth/google` or
        `POST /api/auth/verify-code`, then send it as `Authorization: Bearer <token>`. Access tokens are
        short-lived; exchange the refresh token for a new pair with `POST /api/auth/refresh`.
        Endpoints marked with a lock need a token; the rest are anonymous.

        **Conventions.**
        - JSON with camelCase property names. Dates are ISO 8601 in UTC.
        - Successful responses carry `"success": true`; list and detail endpoints put their payload in `data`.
        - Failed requests answer with `{ "message": ... }` and, where the client should react to a specific
          reason, a stable `code` (see `ApiError`). Show your own text for a `code`; `message` is English.
        - Every client is rate limited per IP (200 requests per minute); beyond that the API answers `429`.
        """;

    public static void Configure(SwaggerGenOptions options)
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "TaleTrack API",
            Version = "v1",
            Description = Description,
        });

        options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token returned by the login endpoints. Paste the token only, without the `Bearer ` prefix.",
        });

        options.OperationFilter<ResponsesOperationFilter>();
        options.DocumentFilter<TagsDocumentFilter>();

        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
        if (File.Exists(xmlPath))
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    /// <summary>The tags used by the endpoints, in the order the UI lists them.</summary>
    internal static readonly (string Name, string Description)[] Tags =
    [
        ("Auth", "Registration, sign-in (password, Google, email code), token refresh, sessions, password reset and account deletion."),
        ("Users", "The caller's own profile and avatar, and other users' public profiles."),
        ("Media", "Detail page of a film, series or book."),
        ("Tracking", "Record and edit progress on films, series and books. Media is created on first tracking."),
        ("Reviews", "Ratings (1-10) and comments written by the caller."),
        ("Library", "Everything the caller is tracking, one row per media."),
        ("Stats", "Yearly consumption summary."),
        ("Friends", "Friend list and friend requests."),
        ("Activity", "Feed of what the caller and their friends started, finished and reviewed."),
    ];
}

/// <summary>Adds what every operation shares: the lock and 401 for protected endpoints, the 429, and the
/// per-endpoint response descriptions.</summary>
public class ResponsesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        operation.Responses ??= [];

        foreach (var described in metadata.OfType<ResponseDescription>())
        {
            if (operation.Responses.TryGetValue(described.StatusCode.ToString(), out var response))
                response.Description = described.Text;
        }

        var requiresAuth = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();
        if (requiresAuth)
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(OpenApiConfiguration.BearerScheme, context.Document)] = []
            });
            operation.Responses.TryAdd("401", new OpenApiResponse
            {
                Description = "The access token is missing, invalid or expired. Refresh it (`POST /api/auth/refresh`) and retry."
            });
        }

        operation.Responses.TryAdd("429", new OpenApiResponse
        {
            Description = "Rate limit exceeded (200 requests per minute per IP)."
        });
    }
}

/// <summary>Registers the tag descriptions and their display order.</summary>
public class TagsDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        document.Tags = new HashSet<OpenApiTag>(
            OpenApiConfiguration.Tags.Select(t => new OpenApiTag { Name = t.Name, Description = t.Description }));
    }
}
