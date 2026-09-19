using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TaleTrackApp.Data;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>An exception nobody catches must surface as a generic ProblemDetails 500.</summary>
[Collection(ApiCollection.Name)]
public class UnhandledExceptionTests(CustomWebApplicationFactory factory)
{
    private const string SecretMessage = "internal-detail-must-not-leak";

    [Fact]
    public async Task UnhandledException_ReturnsProblemDetails500_WithoutLeakingInternals()
    {
        // Login has no try/catch of its own, so a failing query reaches the global handler.
        var interceptor = new ThrowingInterceptor();
        var client = factory
            .WithWebHostBuilder(b => b.ConfigureServices(services =>
                services.ConfigureDbContext<AppDbContext>(o => o.AddInterceptors(interceptor))))
            .CreateClient();

        // Arm only after startup, which already ran EnsureCreated through the same context.
        interceptor.Armed = true;
        var response = await client.PostAsJsonAsync("/api/login",
            new { Email = "boom@test.com", Password = "Password1!" });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecretMessage, body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("TaleTrackApp.", body);
    }

    private sealed class ThrowingInterceptor : DbCommandInterceptor
    {
        public volatile bool Armed;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
            Armed ? throw new InvalidOperationException(SecretMessage) : base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
