using Hazina.Agent.API.Models;

namespace Hazina.Agent.API.Services;

public class BackgroundSyncService : BackgroundService
{
    private readonly ILogger<BackgroundSyncService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5);

    public BackgroundSyncService(
        ILogger<BackgroundSyncService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundSyncService starting (interval: {Interval})", _syncInterval);

        // Wait a bit before first sync (let app initialize)
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformSyncAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background sync failed");
            }

            await Task.Delay(_syncInterval, stoppingToken);
        }

        _logger.LogInformation("BackgroundSyncService stopping");
    }

    private async Task PerformSyncAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();

        var stateSyncService = scope.ServiceProvider.GetRequiredService<IStateSyncService>();
        var learningService = scope.ServiceProvider.GetRequiredService<ILearningIntegrationService>();

        _logger.LogInformation("Starting background sync");

        // Get identity and last sync time
        var identity = await stateSyncService.GetIdentityAsync(ct);
        var lastSync = identity.Instance.LastSync;

        // Sync state (git pull)
        await stateSyncService.SyncStateAsync(ct);

        // Check for conflicts
        var hasConflicts = await stateSyncService.HasConflictsAsync(ct);
        if (hasConflicts)
        {
            _logger.LogWarning("Conflicts detected during background sync, attempting resolution");
            await stateSyncService.ResolveConflictsAsync(ct);
        }

        // Get new learning events from other agents
        var newEvents = await stateSyncService.GetNewLearningEventsAsync(lastSync, ct);

        if (newEvents.Any())
        {
            _logger.LogInformation("Received {Count} new learning events from other agents", newEvents.Count);

            // Group by agent
            var eventsByAgent = newEvents.GroupBy(e => e.AgentId);
            foreach (var group in eventsByAgent)
            {
                _logger.LogInformation("  - {AgentId}: {Count} events", group.Key, group.Count());
            }

            // Integrate into consciousness
            await learningService.IntegrateNewLearningsAsync(newEvents, ct);

            _logger.LogInformation("Successfully integrated {Count} learning events", newEvents.Count);
        }
        else
        {
            _logger.LogInformation("No new learning events since last sync");
        }

        _logger.LogInformation("Background sync completed");
    }
}
