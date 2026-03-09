using Microsoft.AspNetCore.Authorization;

namespace MediaTrackerApp.Auth;

public class InternalApiKeyRequirement : IAuthorizationRequirement
{
}

public class InternalApiKeyHandler : AuthorizationHandler<InternalApiKeyRequirement>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalApiKeyHandler> _logger;

    public InternalApiKeyHandler(IConfiguration configuration, ILogger<InternalApiKeyHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, InternalApiKeyRequirement requirement)
    {
        if (context.Resource is HttpContext httpContext)
        {
            if (!httpContext.Request.Headers.TryGetValue("X-Internal-Api-Key", out var apiKeyFromHeader))
            {
                _logger.LogWarning("Internal API key missing in request");
                return Task.CompletedTask;
            }

            var configuredApiKey = Environment.GetEnvironmentVariable("INTERNAL_API_KEY") ?? _configuration["InternalApiKey"];
            
            if (string.IsNullOrEmpty(configuredApiKey))
            {
                _logger.LogError("Internal API key not configured in server");
                return Task.CompletedTask;
            }

            if (apiKeyFromHeader == configuredApiKey)
            {
                _logger.LogInformation("Internal API key validated successfully");
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("Invalid internal API key provided");
            }
        }

        return Task.CompletedTask;
    }
}
