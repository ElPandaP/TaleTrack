using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TaleTrackApp.Features.User.Login;
using TaleTrackApp.Features.User.Register;
using TaleTrackApp.Features.User.EditUser;
using TaleTrackApp.Features.User.DeleteUser;
using TaleTrackApp.Features.Media.AddMedia;
using TaleTrackApp.Features.Media.GetMedia;
using TaleTrackApp.Features.TrackingEvent.AddTrackingEvent;
using TaleTrackApp.Features.TrackingEvent.GetTrackingEvents;
using TaleTrackApp.Features.Review.AddReview;
using TaleTrackApp.Features.Review.EditReview;
using TaleTrackApp.Features.Review.DeleteReview;
using TaleTrackApp.Features.User;
using TaleTrackApp.Features.Media;
using TaleTrackApp.Features.TrackingEvent;
using TaleTrackApp.Features.Review;
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
}

void configureApi()
{
    builder.Services.AddControllers();
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

void configurePipeline()
{
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
    
    // User endpoints (JWT + API Key)
    AddMediaEndpoint.Map(apiGroup);
    AddTrackingEventEndpoint.Map(apiGroup);
    RegisterEndpoint.Map(apiGroup);
    
    // User data endpoints (JWT)
    GetTrackingEventsEndpoint.Map(apiGroup);
    
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
