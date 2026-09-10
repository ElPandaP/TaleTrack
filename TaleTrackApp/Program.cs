using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TaleTrackApp.Features.User.Login;
using TaleTrackApp.Features.User.Register;
using TaleTrackApp.Features.User.GoogleLogin;
using TaleTrackApp.Features.User.EmailAuth;
using TaleTrackApp.Features.Auth;
using TaleTrackApp.Features.Auth.Refresh;
using TaleTrackApp.Features.Auth.Logout;
using TaleTrackApp.Features.Auth.ExtensionGrant;
using TaleTrackApp.Features.Auth.Sessions;
using TaleTrackApp.Features.User.EditUser;
using TaleTrackApp.Features.User.DeleteUser;
using TaleTrackApp.Features.User.GetMe;
using TaleTrackApp.Features.User.SearchUsers;
using TaleTrackApp.Features.User.GetUserProfile;
using TaleTrackApp.Features.Media.AddMedia;
using TaleTrackApp.Features.Media.GetMedia;
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
using TaleTrackApp.Features.Friend.RespondRequest;
using TaleTrackApp.Features.Friend.RemoveFriend;
using TaleTrackApp.Features.Activity;
using TaleTrackApp.Features.Activity.GetActivity;
using TaleTrackApp.Auth;

// Load environment variables from .env
loadEnvironment();

var builder = WebApplication.CreateBuilder(args);

// Configure services
configureDatabase();
configureAuth();
configureApi();
configureCors();

var app = builder.Build();

configurePipeline();

app.Run();

void loadEnvironment()
{
    string envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
    if(File.Exists(envPath))
    {
        DotNetEnv.Env.Load(envPath);
    }
}

void configureDatabase()
{
    // In "Testing" environment, the test project provides its own DbContext via ConfigureTestServices
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

void configureAuth()
{
    var jwtSecret = builder.Configuration["JwtSettings:Secret"];
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
        })
        .AddPolicy(Policies.InternalOnly, policy =>
        {
            policy.Requirements.Add(new InternalApiKeyRequirement());
        })
        .AddPolicy(Policies.UserAndInternal, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new InternalApiKeyRequirement());
        });

    builder.Services.AddScoped<IAuthorizationHandler, InternalApiKeyHandler>();
    builder.Services.AddScoped<JwtService>();
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<RefreshTokenService>();

    var resendApiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY")!;
    builder.Services.AddHttpClient<EmailService>(client =>
    {
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {resendApiKey}");
    });
}

void configureApi()
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }
    });

    // Register Shared Services (REPR pattern)
    builder.Services.AddScoped<UserService>();
    builder.Services.AddScoped<MediaService>();
    builder.Services.AddScoped<TrackingEventService>();
    builder.Services.AddScoped<ReviewService>();
    builder.Services.AddScoped<StatsService>();
    builder.Services.AddScoped<LibraryService>();
    builder.Services.AddScoped<FriendService>();
    builder.Services.AddScoped<ActivityService>();
    builder.Services.AddHttpClient<OpenLibraryService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });
    builder.Services.AddHttpClient<TmdbService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });

    // Add automatic model validation filter
    builder.Services.AddScoped<IEndpointFilter, ValidationFilter>();
}

void configureCors()
{
    var allowedOrigins = (Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS") ?? "http://localhost:8090")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("FrontendCors", policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

void applyMigrations()
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TaleTrackApp.Data.AppDbContext>();
    if (db.Database.ProviderName?.Contains("Sqlite") == true)
        db.Database.EnsureCreated();
    else
        db.Database.Migrate();
}

void configurePipeline()
{
    applyMigrations();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
    app.UseCors("FrontendCors");
    
    app.UseAuthentication();
    app.UseAuthorization();
    
    // Map endpoints by feature (REPR pattern)
    var apiGroup = app.MapGroup("/api").WithName("API");
    
    // Public endpoints
    LoginEndpoint.Map(apiGroup);
    GoogleLoginEndpoint.Map(apiGroup);
    RequestCodeEndpoint.Map(apiGroup);
    VerifyCodeEndpoint.Map(apiGroup);
    RefreshEndpoint.Map(apiGroup);
    LogoutEndpoint.Map(apiGroup);

    // Session / token management (JWT)
    ExtensionGrantEndpoint.Map(apiGroup);
    GetSessionsEndpoint.Map(apiGroup);
    RevokeSessionEndpoint.Map(apiGroup);
    
    // User endpoints (JWT + API Key)
    AddMediaEndpoint.Map(apiGroup);
    TrackMovieEndpoint.Map(apiGroup);
    TrackSeriesEndpoint.Map(apiGroup);
    TrackBookEndpoint.Map(apiGroup);
    DeleteTrackingEndpoint.Map(apiGroup);
    EditTrackingProgressEndpoint.Map(apiGroup);
    RegisterEndpoint.Map(apiGroup);

    // User data endpoints (JWT)
    GetStatsEndpoint.Map(apiGroup);
    GetLibraryEndpoint.Map(apiGroup);
    GetReviewsEndpoint.Map(apiGroup);
    GetPendingReviewsEndpoint.Map(apiGroup);
    GetMediaByIdEndpoint.Map(apiGroup);
    GetMeEndpoint.Map(apiGroup);
    SearchUsersEndpoint.Map(apiGroup);
    GetUserProfileEndpoint.Map(apiGroup);

    // Friends & activity (JWT)
    GetFriendsEndpoint.Map(apiGroup);
    SendFriendRequestEndpoint.Map(apiGroup);
    RespondFriendRequestEndpoint.Map(apiGroup);
    RemoveFriendEndpoint.Map(apiGroup);
    GetActivityEndpoint.Map(apiGroup);

    // Internal API endpoints (API Key)
    GetMediaEndpoint.Map(apiGroup);
    
    // User management endpoints (JWT + API Key)
    EditUserEndpoint.Map(apiGroup);
    DeleteUserEndpoint.Map(apiGroup);
    
    // Review endpoints (JWT + API Key)
    AddReviewEndpoint.Map(apiGroup);
    EditReviewEndpoint.Map(apiGroup);
    DeleteReviewEndpoint.Map(apiGroup);
}

public partial class Program { }
