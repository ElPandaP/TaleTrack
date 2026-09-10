using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using TaleTrackApp.Auth;
using TaleTrackApp.Data;
using TaleTrackApp.Features.Media;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Shares one factory (and thus one in-memory SQLite DB) across every test class.
/// The factory disposes its keep-alive connection on teardown, so multiple
/// independent <c>IClassFixture</c> instances would tear the DB down mid-run.
/// </summary>
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    public const string Name = "Api";
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Named in-memory SQLite DB — persists as long as KeepAlive connection is open
    private static readonly string DbName = $"taletrack_test_{Guid.NewGuid():N}";
    private static readonly SqliteConnection KeepAlive;

    public const string TestInternalApiKey = "test-internal-api-key";
    private const string TestJwtSecret = "test-jwt-secret-key-minimum-32-characters!!";

    static CustomWebApplicationFactory()
    {
        // Set env vars before Program.Main() runs (it runs lazily on first CreateClient())
        // configureAuth() reads JwtSettings:Secret eagerly from builder.Configuration,
        // which picks up JwtSettings__Secret (double-underscore = hierarchy in .NET config)
        Environment.SetEnvironmentVariable("JwtSettings__Secret", TestJwtSecret);
        Environment.SetEnvironmentVariable("INTERNAL_API_KEY", TestInternalApiKey);
        // Fake key so TmdbService doesn't short-circuit before making a (stubbed) HTTP
        // call — exercises the same code path production traffic takes.
        Environment.SetEnvironmentVariable("TMDB_API_KEY", "test-tmdb-api-key");

        KeepAlive = new SqliteConnection($"DataSource={DbName};Mode=Memory;Cache=Shared");
        KeepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Skip Npgsql registration (see configureDatabase() in Program.cs)
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Provide SQLite in-memory DbContext (Npgsql was never registered)
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"DataSource={DbName};Mode=Memory;Cache=Shared"));

            // Stub OpenLibrary HTTP calls — enrichment is fire-and-forget,
            // tests only verify the synchronous tracking path
            services.PostConfigure<HttpClientFactoryOptions>(
                typeof(OpenLibraryService).FullName!,
                opts =>
                {
                    opts.HttpMessageHandlerBuilderActions.Clear();
                    opts.HttpMessageHandlerBuilderActions.Add(b =>
                        b.PrimaryHandler = new StubHttpMessageHandler());
                });

            // Same for TMDB — never hit the real API from tests.
            services.PostConfigure<HttpClientFactoryOptions>(
                typeof(TmdbService).FullName!,
                opts =>
                {
                    opts.HttpMessageHandlerBuilderActions.Clear();
                    opts.HttpMessageHandlerBuilderActions.Add(b =>
                        b.PrimaryHandler = new StubHttpMessageHandler());
                });

            // Never hit Resend from tests — pretend every send succeeds.
            services.AddHttpClient<EmailService>()
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new StubHttpMessageHandler(System.Net.HttpStatusCode.OK));
        });
    }

    /// <summary>A scoped <see cref="AppDbContext"/> for tests to seed or assert against.</summary>
    public IServiceScope NewDbScope(out AppDbContext db)
    {
        var scope = Services.CreateScope();
        db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return scope;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) KeepAlive.Dispose();
    }

    private sealed class StubHttpMessageHandler(
        System.Net.HttpStatusCode status = System.Net.HttpStatusCode.NotFound) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status));
    }
}
