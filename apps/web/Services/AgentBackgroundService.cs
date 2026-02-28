using BuddyAgent.AgentLib;

namespace BuddyAgent.Web.Services;

public class AgentBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<AgentBackgroundService> logger,
    IConfiguration config) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Agent background service started. Interval: {Interval}", Interval);

        // Run once at startup (delay slightly so the app is fully ready)
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        await RunAgentAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunAgentAsync(stoppingToken);
        }
    }

    private async Task RunAgentAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();

            var tasksPath = config["AgentLib:TasksPath"]
                ?? Path.Combine(AppContext.BaseDirectory, "tasks.json");

            logger.LogInformation("Starting agent run using tasks from: {Path}", tasksPath);
            var results = await runner.RunAllTasksAsync(tasksPath, ct);
            logger.LogInformation("Agent run completed. {Count} task(s) processed.", results.Count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agent run failed");
        }
    }
}
