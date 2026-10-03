namespace TaleTrackApp.Services;

/// <summary>
/// Runs work after the response is sent (welcome emails, metadata enrichment...), in its own DI
/// scope because the request's scope is disposed by then. Failures are logged, never thrown.
/// </summary>
/// <param name="scopeFactory">Creates the scope each piece of work runs in.</param>
/// <param name="logger">Logs the failures.</param>
public class BackgroundRunner(IServiceScopeFactory scopeFactory, ILogger<BackgroundRunner> logger)
{
    /// <summary>
    /// Starts <paramref name="work"/> on the thread pool and returns immediately, without waiting
    /// for it to finish.
    /// </summary>
    /// <typeparam name="TService">Service resolved from the new scope and handed to the work.</typeparam>
    /// <param name="description">Short label used in the error log if the work fails.</param>
    /// <param name="work">The work to run.</param>
    public void Run<TService>(string description, Func<TService, Task> work) where TService : notnull
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await work(scope.ServiceProvider.GetRequiredService<TService>());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background task failed: {Description}", description);
            }
        });
    }
}
