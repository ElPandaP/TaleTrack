using System.Text.Json;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using TaleTrackApp.Data;
using TaleTrackApp.Features.Auth;
using TaleTrackApp.Features.Media;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// The xUnit collection every integration test class joins, so they all share one
/// <see cref="CustomWebApplicationFactory"/> and therefore one in-memory database.
/// The factory closes the database when it is disposed, so giving each class its own
/// factory would wipe the database while other classes are still running.
/// </summary>
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    public const string Name = "Api";
}

/// <summary>
/// Starts the real backend in memory for the integration tests. It swaps PostgreSQL for an
/// in-memory SQLite database, sets a test JWT secret, and replaces TMDB, Open Library, Resend and
/// Google's id token validation with fakes (see ExternalServiceFakes.cs), so no test ever reaches
/// an external service. Tests tell the fakes what to answer through <see cref="Tmdb"/>,
/// <see cref="OpenLibrary"/> and <see cref="Resend"/>.
/// Because the database and the fakes are shared by every test, each test registers its own users
/// and uses its own titles to avoid seeing another test's data.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // A named in-memory SQLite database lives only while at least one connection to it is open,
    // so KeepAlive stays open for the whole run.
    private static readonly string DbName = $"taletrack_test_{Guid.NewGuid():N}";
    private static readonly SqliteConnection KeepAlive;

    private const string TestJwtSecret = "test-jwt-secret-key-minimum-32-characters!!";

    static CustomWebApplicationFactory()
    {
        // Environment variables must be set before Program.Main() runs, which happens on the
        // first CreateClient(). configureAuth() reads JwtSettings:Secret straight away, and .NET
        // configuration maps JwtSettings__Secret (double underscore) onto that key.
        Environment.SetEnvironmentVariable("JwtSettings__Secret", TestJwtSecret);
        // A fake key keeps TmdbService from skipping the lookup, so tests go through the same
        // code path as production; the HTTP call itself hits the stub below.
        Environment.SetEnvironmentVariable("TMDB_API_KEY", "test-tmdb-api-key");
        // Google sign-in is off without a client id; the fake validator ignores its value.
        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", "test-google-client-id");

        KeepAlive = new SqliteConnection($"DataSource={DbName};Mode=Memory;Cache=Shared");
        KeepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The "Testing" environment makes Program.cs skip the PostgreSQL registration and the
        // global rate limiter.
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // The in-memory SQLite database replaces PostgreSQL.
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"DataSource={DbName};Mode=Memory;Cache=Shared"));

            // Open Library and TMDB answer from the fakes: a title only exists if a test added it.
            // A typed client is registered under its class's short name.
            services.PostConfigure<HttpClientFactoryOptions>(
                nameof(OpenLibraryService),
                opts =>
                {
                    opts.HttpMessageHandlerBuilderActions.Clear();
                    opts.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = OpenLibrary.Handler());
                });
            services.PostConfigure<HttpClientFactoryOptions>(
                nameof(TmdbService),
                opts =>
                {
                    opts.HttpMessageHandlerBuilderActions.Clear();
                    opts.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = Tmdb.Handler());
                });

            // Emails go to the fake Resend, which keeps them for the tests to read.
            services.AddHttpClient<EmailService>()
                .ConfigurePrimaryHttpMessageHandler(() => Resend.Handler());

            // Google id tokens are checked by the fake instead of Google.
            services.RemoveAll<GoogleIdTokenValidator>();
            services.AddSingleton<GoogleIdTokenValidator>(new FakeGoogleIdTokenValidator());
        });
    }

    /// <summary>Fake TMDB shared by every test.</summary>
    public FakeTmdb Tmdb { get; } = new();

    /// <summary>Fake Open Library shared by every test.</summary>
    public FakeOpenLibrary OpenLibrary { get; } = new();

    /// <summary>Fake Resend shared by every test; it keeps the emails sent.</summary>
    public FakeResend Resend { get; } = new();

    /// <summary>A scoped <see cref="AppDbContext"/> for tests to seed or assert against.</summary>
    public IServiceScope NewDbScope(out AppDbContext db)
    {
        var scope = Services.CreateScope();
        db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return scope;
    }

    /// <summary>
    /// Registers a user, signs them in and returns a client that sends their access token. Fails
    /// the test right away if registering or signing in does not work.
    /// </summary>
    /// <param name="email">Email of the new user; unique per test.</param>
    /// <param name="username">Username of the new user; unique per test.</param>
    public async Task<TestUser> CreateUserAsync(string email, string username)
    {
        var client = CreateClient();
        var register = await client.PostAsJsonAsync("/api/register",
            new { Email = email, Username = username, Password = TestPassword });
        Assert.True(register.IsSuccessStatusCode, await register.Content.ReadAsStringAsync());

        var login = await client.PostAsJsonAsync("/api/login", new { Email = email, Password = TestPassword });
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/users/me");
        var id = me.GetProperty("data").GetProperty("id").GetGuid();
        return new TestUser(client, id, token, body.GetProperty("refreshToken").GetString()!);
    }

    /// <summary>
    /// Issues an email-link token directly through the app's service, for the cases where the
    /// test needs a token the app would not email in that situation (such as one of another purpose).
    /// </summary>
    public async Task<string> IssueActionTokenAsync(Guid userId, string purpose)
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AuthActionTokenService>().IssueAsync(userId, purpose);
    }

    /// <summary>
    /// Changes the stored row of an email-link token, for example to make it expired without
    /// waiting for its lifetime to pass.
    /// </summary>
    public async Task UpdateActionTokenAsync(string rawToken, Action<Model.AuthActionToken> change)
    {
        using var scope = NewDbScope(out var db);
        var hash = TokenHasher.Hash(rawToken);
        change(await db.AuthActionTokens.SingleAsync(t => t.TokenHash == hash));
        await db.SaveChangesAsync();
    }

    /// <summary>Password of every user made with <see cref="CreateUserAsync"/>.</summary>
    public const string TestPassword = "Password1!";

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) KeepAlive.Dispose();
    }
}

/// <summary>A signed-in test user: a client that sends their access token, their id and their tokens.</summary>
public record TestUser(HttpClient Client, Guid Id, string AccessToken, string RefreshToken);
