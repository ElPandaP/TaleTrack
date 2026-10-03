using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using TaleTrackApp.Features.Auth.Login;
using TaleTrackApp.Features.Auth.Register;
using TaleTrackApp.Features.Auth.GoogleLogin;
using TaleTrackApp.Features.Auth.GoogleSignupComplete;
using TaleTrackApp.Features.Auth.RequestCode;
using TaleTrackApp.Features.Auth.VerifyCode;
using TaleTrackApp.Features.Auth;
using TaleTrackApp.Features.Auth.Refresh;
using TaleTrackApp.Features.Auth.Logout;
using TaleTrackApp.Features.Auth.ExtensionGrant;
using TaleTrackApp.Features.Auth.GetSessions;
using TaleTrackApp.Features.Auth.RevokeSession;
using TaleTrackApp.Features.Auth.RequestPasswordReset;
using TaleTrackApp.Features.Auth.ResetPassword;
using TaleTrackApp.Features.Auth.ConfirmDelete;
using TaleTrackApp.Features.Auth.RevokeSignup;
using TaleTrackApp.Features.User.EditUser;
using TaleTrackApp.Features.Auth.RequestDeletion;
using TaleTrackApp.Features.User.GetAvatar;
using TaleTrackApp.Features.User.UploadAvatar;
using TaleTrackApp.Features.User.DeleteAvatar;
using TaleTrackApp.Features.User.GetMe;
using TaleTrackApp.Features.User.SearchUsers;
using TaleTrackApp.Features.User.GetUserProfile;
using TaleTrackApp.Features.Media.GetMediaById;
using TaleTrackApp.Features.TrackingEvent.TrackMovie;
using TaleTrackApp.Features.TrackingEvent.TrackSeries;
using TaleTrackApp.Features.TrackingEvent.TrackBook;
using TaleTrackApp.Features.TrackingEvent.DeleteTracking;
using TaleTrackApp.Features.TrackingEvent.EditTrackingProgress;
using TaleTrackApp.Features.Review.AddReview;
using TaleTrackApp.Features.Review.EditReview;
using TaleTrackApp.Features.Review.DeleteReview;
using TaleTrackApp.Features.Review.GetReviews;
using TaleTrackApp.Features.Review.GetPendingReviews;
using TaleTrackApp.Features.User;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Features.TrackingEvent;
using TaleTrackApp.Features.Review;
using TaleTrackApp.Features.Stats;
using TaleTrackApp.Features.Stats.GetStats;
using TaleTrackApp.Features.Library;
using TaleTrackApp.Features.Library.GetLibrary;
using TaleTrackApp.Features.Friend;
using TaleTrackApp.Features.Friend.GetFriends;
using TaleTrackApp.Features.Friend.SendRequest;
using TaleTrackApp.Features.Friend.AcceptRequest;
using TaleTrackApp.Features.Friend.DeclineRequest;
using TaleTrackApp.Features.Friend.RemoveFriend;
using TaleTrackApp.Features.Activity;
using TaleTrackApp.Features.Activity.GetActivity;
using TaleTrackApp.OpenApi;
using TaleTrackApp.Security;
using TaleTrackApp.Services;

// Application entry point: registers services, builds the app, applies pending migrations,
// sets up the middleware pipeline and maps every endpoint under /api.

// The .env file must be loaded before the builder reads configuration from the environment.
loadEnvironment();

var builder = WebApplication.CreateBuilder(args);

configureDatabase();
configureAuth();
configureApi();
configureRateLimiting();
configureRequestLimits();

var app = builder.Build();

// Without it every email (codes, password resets, account deletion) fails, but the rest works.
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RESEND_API_KEY")))
    app.Logger.LogWarning("RESEND_API_KEY is not set: no email can be sent");

applyMigrations();
configurePipeline();

app.Run();

// Loads ../.env (relative to the working directory) when it exists; in Docker the variables
// come from the container environment instead.
void loadEnvironment()
{
    string envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
    if(File.Exists(envPath))
    {
        DotNetEnv.Env.Load(envPath);
    }
}

// Registers the PostgreSQL DbContext from the POSTGRES_* environment variables.
void configureDatabase()
{
    // The test project registers its own SQLite DbContext through ConfigureTestServices.
    if (builder.Environment.EnvironmentName == "Testing") return;

    var dbHost = Environment.GetEnvironmentVariable("POSTGRES_HOST");
    var dbPort = Environment.GetEnvironmentVariable("POSTGRES_PORT");
    var dbName = Environment.GetEnvironmentVariable("POSTGRES_DB");
    var dbUser = Environment.GetEnvironmentVariable("POSTGRES_USER");
    var dbPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

    var connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";
    builder.Services.AddDbContext<TaleTrackApp.Data.AppDbContext>(options =>
        options.UseNpgsql(connectionString)
    );
}

// JWT bearer authentication, the authorization policy and the services behind the auth
// features (sessions, email-link tokens, Google sign-in, email sending, background work).
void configureAuth()
{
    var jwtSecret = builder.Configuration["JwtSettings:Secret"];
    // `dotnet ef` also runs this file to build the model, without any secret configured.
    if (string.IsNullOrEmpty(jwtSecret) && !EF.IsDesignTime)
        throw new InvalidOperationException("JwtSettings:Secret is not configured (set JWT_SECRET).");
    var jwtIssuer = builder.Configuration["JwtSettings:Issuer"];
    var jwtAudience = builder.Configuration["JwtSettings:Audience"];

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                // Access tokens live only a few minutes, so a revoked session stops working soon after;
                // the 5-minute default skew would double that window.
                ClockSkew = TimeSpan.FromSeconds(30),
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(jwtSecret!))
            };
        });

    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(Policies.UserPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
        });

    builder.Services.AddScoped<JwtService>();
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<SessionService>();
    builder.Services.AddScoped<AuthActionTokenService>();
    builder.Services.AddScoped<GoogleSignupTokenService>();
    builder.Services.AddScoped<GoogleAuthService>();
    builder.Services.AddSingleton<GoogleIdTokenValidator>();
    builder.Services.AddSingleton<BackgroundRunner>();

    var resendApiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY");
    builder.Services.AddHttpClient<EmailService>(client =>
    {
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {resendApiKey}");
    });
}

// Swagger/OpenAPI, the scoped feature services and the typed HTTP clients for the external
// metadata providers (Open Library and TMDB).
void configureApi()
{
    builder.Services.AddProblemDetails();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(OpenApiConfiguration.Configure);

    // Services shared by the endpoints of each feature.
    builder.Services.AddScoped<UserService>();
    builder.Services.AddScoped<MediaService>();
    builder.Services.AddScoped<TrackingEventService>();
    builder.Services.AddScoped<ReviewService>();
    builder.Services.AddScoped<StatsService>();
    builder.Services.AddScoped<LibraryService>();
    builder.Services.AddScoped<FriendService>();
    builder.Services.AddScoped<ActivityService>();
    builder.Services.AddScoped<AvatarService>();
    builder.Services.AddHttpClient<OpenLibraryService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
        // Open Library asks API clients to identify themselves.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TaleTrack/1.0 (+https://taletrack.app)");
    });
    builder.Services.AddHttpClient<TmdbService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });
}

// Global per-IP rate limit (200 requests per minute by default, RateLimiting:PermitLimit), using the
// client IP forwarded by the proxy.
void configureRateLimiting()
{
    // The backend is only reachable through the Caddy reverse proxy (Docker network, no
    // published port), so X-Forwarded-For from any peer can be trusted here.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // The in-memory test server has no real client IPs and fires far more requests per
        // second than any real client would, so the integration test suite would trip this. Only
        // the test that checks the limiter turns it on, by setting its own limit.
        var permitLimit = builder.Configuration.GetValue<int?>("RateLimiting:PermitLimit");
        if (builder.Environment.EnvironmentName == "Testing" && permitLimit is null) return;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(httpContext =>
        {
            var ip = httpContext.Connection.RemoteIpAddress ?? IPAddress.None;
            return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit ?? 200,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
        });
    });
}

// Caps the request body size accepted by Kestrel.
void configureRequestLimits()
{
    // Headroom over the 5 MB avatar upload cap (UploadAvatarEndpoint.MaxBytes); every other
    // request body is small JSON. Rejects oversized bodies at the transport level instead of
    // buffering them into memory first.
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 8 * 1024 * 1024;
    });
}

// Brings the database schema up to date on startup. SQLite (used by the tests) cannot run the
// PostgreSQL migrations, so the schema is created straight from the model there.
void applyMigrations()
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TaleTrackApp.Data.AppDbContext>();
    if (db.Database.ProviderName?.Contains("Sqlite") == true)
        db.Database.EnsureCreated();
    else
        db.Database.Migrate();
}

// Middleware order and endpoint registration.
void configurePipeline()
{
    // Outermost, so any unhandled exception becomes a logged ProblemDetails 500 without leaking internals
    app.UseExceptionHandler();
    app.UseForwardedHeaders();

    // Basic security headers on every response.
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Referrer-Policy", "no-referrer");
        await next();
    });

    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    // Every endpoint lives under /api and is mapped by its feature's static Map method.
    var apiGroup = app.MapGroup("/api");

    // Public endpoints (anonymous)
    RegisterEndpoint.Map(apiGroup);
    LoginEndpoint.Map(apiGroup);
    GoogleLoginEndpoint.Map(apiGroup);
    GoogleSignupCompleteEndpoint.Map(apiGroup);
    RequestCodeEndpoint.Map(apiGroup);
    VerifyCodeEndpoint.Map(apiGroup);
    RefreshEndpoint.Map(apiGroup);
    LogoutEndpoint.Map(apiGroup);
    RequestPasswordResetEndpoint.Map(apiGroup);
    ResetPasswordEndpoint.Map(apiGroup);
    ConfirmDeleteEndpoint.Map(apiGroup);
    RevokeSignupEndpoint.Map(apiGroup);
    GetAvatarEndpoint.Map(apiGroup);

    // Session / token management (JWT)
    ExtensionGrantEndpoint.Map(apiGroup);
    GetSessionsEndpoint.Map(apiGroup);
    RevokeSessionEndpoint.Map(apiGroup);

    // Account & profiles (JWT)
    GetMeEndpoint.Map(apiGroup);
    EditUserEndpoint.Map(apiGroup);
    RequestAccountDeletionEndpoint.Map(apiGroup);
    UploadAvatarEndpoint.Map(apiGroup);
    DeleteAvatarEndpoint.Map(apiGroup);
    SearchUsersEndpoint.Map(apiGroup);
    GetUserProfileEndpoint.Map(apiGroup);

    // Media & tracking (JWT)
    GetMediaByIdEndpoint.Map(apiGroup);
    TrackMovieEndpoint.Map(apiGroup);
    TrackSeriesEndpoint.Map(apiGroup);
    TrackBookEndpoint.Map(apiGroup);
    EditTrackingProgressEndpoint.Map(apiGroup);
    DeleteTrackingEndpoint.Map(apiGroup);

    // Reviews (JWT)
    GetReviewsEndpoint.Map(apiGroup);
    GetPendingReviewsEndpoint.Map(apiGroup);
    AddReviewEndpoint.Map(apiGroup);
    EditReviewEndpoint.Map(apiGroup);
    DeleteReviewEndpoint.Map(apiGroup);

    // Library & stats (JWT)
    GetLibraryEndpoint.Map(apiGroup);
    GetStatsEndpoint.Map(apiGroup);

    // Friends & activity (JWT)
    GetFriendsEndpoint.Map(apiGroup);
    SendFriendRequestEndpoint.Map(apiGroup);
    AcceptFriendRequestEndpoint.Map(apiGroup);
    DeclineFriendRequestEndpoint.Map(apiGroup);
    RemoveFriendEndpoint.Map(apiGroup);
    GetActivityEndpoint.Map(apiGroup);
}

/// <summary>
/// Entry point of the backend. The body is written as top-level statements; this partial
/// declaration makes the class public so the integration tests can host the app with
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program { }
