namespace Hazina.Web.Dashboard.Services;

/// <summary>
/// Metrics collector - background service that tracks system performance
/// Broadcasts real-time metrics to dashboard clients
/// </summary>
public class MetricsCollector : IHostedService, IDisposable
{
    private readonly DashboardService _dashboardService;
    private readonly ILogger<MetricsCollector> _logger;
    private Timer? _timer;
    private readonly DateTime _startTime;

    // Metrics tracking
    private long _totalEventsProcessed;
    private long _totalAgentsSpawned;
    private long _successfulCompletions;
    private long _failedCompletions;
    private readonly List<double> _executionTimes = new();
    private readonly object _lock = new();

    // Event tracking for rate calculation
    private long _lastEventCount;
    private DateTime _lastEventCheck;

    public MetricsCollector(
        DashboardService dashboardService,
        ILogger<MetricsCollector> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
        _startTime = DateTime.UtcNow;
        _lastEventCheck = DateTime.UtcNow;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Metrics Collector starting");

        // Collect and broadcast metrics every 5 seconds
        _timer = new Timer(
            CollectAndBroadcast,
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(5));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Metrics Collector stopping");

        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    /// <summary>
    /// Collect metrics and broadcast to clients
    /// </summary>
    private async void CollectAndBroadcast(object? state)
    {
        try
        {
            var metrics = GetCurrentMetrics();
            await _dashboardService.BroadcastMetricsAsync(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect and broadcast metrics");
        }
    }

    /// <summary>
    /// Get current metrics snapshot
    /// </summary>
    private DashboardMetrics GetCurrentMetrics()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var timeSinceLastCheck = (now - _lastEventCheck).TotalMinutes;
            var eventsPerMinute = timeSinceLastCheck > 0
                ? (long)((_totalEventsProcessed - _lastEventCount) / timeSinceLastCheck)
                : 0;

            _lastEventCount = _totalEventsProcessed;
            _lastEventCheck = now;

            var avgExecutionTime = _executionTimes.Count > 0
                ? _executionTimes.Average()
                : 0.0;

            var uptime = (now - _startTime).TotalSeconds;

            return new DashboardMetrics
            {
                Timestamp = now,
                TotalEventsProcessed = _totalEventsProcessed,
                EventsPerMinute = eventsPerMinute,
                TotalAgentsSpawned = _totalAgentsSpawned,
                SuccessfulCompletions = _successfulCompletions,
                FailedCompletions = _failedCompletions,
                AverageExecutionTime = avgExecutionTime,
                SystemUptime = uptime
            };
        }
    }

    /// <summary>
    /// Record an event processed
    /// </summary>
    public void RecordEvent()
    {
        lock (_lock)
        {
            _totalEventsProcessed++;
        }
    }

    /// <summary>
    /// Record an agent spawned
    /// </summary>
    public void RecordAgentSpawned()
    {
        lock (_lock)
        {
            _totalAgentsSpawned++;
        }
    }

    /// <summary>
    /// Record agent completion
    /// </summary>
    public void RecordCompletion(bool success, double executionTimeSeconds)
    {
        lock (_lock)
        {
            if (success)
            {
                _successfulCompletions++;
            }
            else
            {
                _failedCompletions++;
            }

            _executionTimes.Add(executionTimeSeconds);

            // Keep only last 100 execution times for average calculation
            if (_executionTimes.Count > 100)
            {
                _executionTimes.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Reset all metrics
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _totalEventsProcessed = 0;
            _totalAgentsSpawned = 0;
            _successfulCompletions = 0;
            _failedCompletions = 0;
            _executionTimes.Clear();
            _lastEventCount = 0;
            _lastEventCheck = DateTime.UtcNow;
        }

        _logger.LogInformation("Metrics reset");
    }
}
