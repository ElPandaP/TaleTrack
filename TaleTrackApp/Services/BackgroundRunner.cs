namespace TaleTrackApp.Services;

/// <summary>Runs work after the response is sent, in its own DI scope (the request's scope is
/// disposed by then). Failures are logged, never thrown.</summary>
public class BackgroundRunner(IServiceScopeFactory scopeFactory, ILogger<BackgroundRunner> logger)
{
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
