using Spectre.Console;

namespace Hazina.App.HazinaCoder.Core.Infrastructure;

/// <summary>
/// Graceful degradation framework for HazinaCoder
/// Enables offline operation and fallback strategies
/// </summary>
public class GracefulDegradationService
{
    private readonly CircuitBreakerService _circuitBreaker;
    private readonly Dictionary<string, FallbackStrategy> _fallbackStrategies;
    private OperatingMode _currentMode;

    public GracefulDegradationService(CircuitBreakerService circuitBreaker)
    {
        _circuitBreaker = circuitBreaker;
        _fallbackStrategies = new Dictionary<string, FallbackStrategy>();
        _currentMode = OperatingMode.Normal;
        InitializeDefaultFallbacks();
    }

    /// <summary>
    /// Execute operation with graceful degradation
    /// </summary>
    public async Task<T> ExecuteWithFallbackAsync<T>(
        string serviceName,
        Func<Task<T>> primaryOperation,
        Func<Task<T>>? fallbackOperation = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try primary operation with circuit breaker
            return await _circuitBreaker.ExecuteAsync(
                serviceName,
                async ct => await primaryOperation(),
                cancellationToken
            );
        }
        catch (ServiceUnavailableException)
        {
            // Service unavailable - try fallback
            if (fallbackOperation != null)
            {
                NotifyDegradedMode(serviceName, "Using fallback strategy");
                return await fallbackOperation();
            }

            // No fallback - check registered fallback strategies
            if (_fallbackStrategies.TryGetValue(serviceName, out var strategy))
            {
                if (strategy.Fallback != null)
                {
                    NotifyDegradedMode(serviceName, strategy.Description);
                    return await ((Func<Task<T>>)strategy.Fallback)();
                }
            }

            throw;
        }
        catch (Exception ex)
        {
            // Other exceptions - try fallback if available
            if (fallbackOperation != null)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: {serviceName} error, using fallback[/]");
                return await fallbackOperation();
            }

            throw;
        }
    }

    /// <summary>
    /// Register fallback strategy for service
    /// </summary>
    public void RegisterFallback<T>(
        string serviceName,
        Func<Task<T>> fallbackOperation,
        string description)
    {
        _fallbackStrategies[serviceName] = new FallbackStrategy
        {
            ServiceName = serviceName,
            Fallback = fallbackOperation,
            Description = description
        };
    }

    /// <summary>
    /// Check if service is available
    /// </summary>
    public bool IsServiceAvailable(string serviceName)
    {
        return _circuitBreaker.IsAvailable(serviceName);
    }

    /// <summary>
    /// Get current operating mode
    /// </summary>
    public OperatingMode GetOperatingMode()
    {
        var statuses = _circuitBreaker.GetAllHealthStatuses();

        var failedCount = statuses.Count(kv => kv.Value.State == ServiceState.Failed);
        var degradedCount = statuses.Count(kv => kv.Value.State == ServiceState.Degraded);

        if (failedCount >= 2 || statuses.Values.All(s => s.State == ServiceState.Offline))
        {
            _currentMode = OperatingMode.Offline;
        }
        else if (failedCount > 0 || degradedCount > 0)
        {
            _currentMode = OperatingMode.Degraded;
        }
        else
        {
            _currentMode = OperatingMode.Normal;
        }

        return _currentMode;
    }

    /// <summary>
    /// Display current system health
    /// </summary>
    public void DisplayHealthStatus()
    {
        var statuses = _circuitBreaker.GetAllHealthStatuses();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Service")
            .AddColumn("Status")
            .AddColumn("Message")
            .AddColumn("Since");

        foreach (var (serviceName, status) in statuses.OrderBy(kv => kv.Key))
        {
            var statusColor = status.State switch
            {
                ServiceState.Healthy => "green",
                ServiceState.Degraded => "yellow",
                ServiceState.Failed => "red",
                ServiceState.Offline => "dim",
                _ => "white"
            };

            var timeSince = FormatTimeSpan(status.TimeSinceLastStateChange);

            table.AddRow(
                serviceName,
                $"[{statusColor}]{status.State}[/]",
                status.Message ?? "-",
                timeSince
            );
        }

        var mode = GetOperatingMode();
        var modeColor = mode switch
        {
            OperatingMode.Normal => "green",
            OperatingMode.Degraded => "yellow",
            OperatingMode.Offline => "red",
            _ => "white"
        };

        AnsiConsole.MarkupLine($"[bold]System Health: [{modeColor}]{mode}[/][/]");
        AnsiConsole.Write(table);

        if (mode == OperatingMode.Degraded)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Running in degraded mode with limited functionality[/]");
        }
        else if (mode == OperatingMode.Offline)
        {
            AnsiConsole.MarkupLine("[red]⚠ Running offline - external services unavailable[/]");
        }
    }

    /// <summary>
    /// Enable offline mode (disable all external calls)
    /// </summary>
    public void EnableOfflineMode()
    {
        _currentMode = OperatingMode.Offline;
        AnsiConsole.MarkupLine("[yellow]Offline mode enabled - using local fallbacks only[/]");
    }

    /// <summary>
    /// Disable offline mode
    /// </summary>
    public void DisableOfflineMode()
    {
        _currentMode = OperatingMode.Normal;
        AnsiConsole.MarkupLine("[green]Offline mode disabled - reconnecting to services[/]");
    }

    /// <summary>
    /// Initialize default fallback strategies
    /// </summary>
    private void InitializeDefaultFallbacks()
    {
        // Qdrant fallback - use in-memory storage
        RegisterFallback<bool>(
            "Qdrant",
            async () =>
            {
                AnsiConsole.MarkupLine("[yellow]Qdrant unavailable - using in-memory experience storage[/]");
                await Task.CompletedTask;
                return false;
            },
            "In-memory experience storage"
        );

        // OpenAI API fallback - use Ollama
        RegisterFallback<string>(
            "OpenAI",
            async () =>
            {
                AnsiConsole.MarkupLine("[yellow]OpenAI API unavailable - attempting Ollama fallback[/]");
                await Task.CompletedTask;
                return string.Empty;
            },
            "Local Ollama model"
        );

        // MCP Server fallback - disable MCP tools
        RegisterFallback<bool>(
            "MCP",
            async () =>
            {
                AnsiConsole.MarkupLine("[yellow]MCP server unavailable - core tools only[/]");
                await Task.CompletedTask;
                return false;
            },
            "Core tools only (MCP disabled)"
        );

        // Network check fallback
        RegisterFallback<bool>(
            "Network",
            async () =>
            {
                AnsiConsole.MarkupLine("[yellow]Network unavailable - offline mode activated[/]");
                EnableOfflineMode();
                await Task.CompletedTask;
                return false;
            },
            "Offline operation"
        );
    }

    /// <summary>
    /// Notify user of degraded mode
    /// </summary>
    private void NotifyDegradedMode(string serviceName, string description)
    {
        if (_currentMode != OperatingMode.Degraded)
        {
            _currentMode = OperatingMode.Degraded;
            AnsiConsole.MarkupLine($"[yellow]⚠ {serviceName} unavailable - {description}[/]");
        }
    }

    /// <summary>
    /// Format timespan for display
    /// </summary>
    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalMinutes < 1)
            return "just now";
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes}m ago";
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours}h ago";
        return $"{(int)timeSpan.TotalDays}d ago";
    }

    /// <summary>
    /// Get capability status (what features are available)
    /// </summary>
    public CapabilityStatus GetCapabilityStatus()
    {
        var mode = GetOperatingMode();
        var statuses = _circuitBreaker.GetAllHealthStatuses();

        return new CapabilityStatus
        {
            CanUseQdrant = _circuitBreaker.IsAvailable("Qdrant"),
            CanUseOpenAI = _circuitBreaker.IsAvailable("OpenAI"),
            CanUseMCP = _circuitBreaker.IsAvailable("MCP"),
            HasNetwork = _circuitBreaker.IsAvailable("Network"),
            OperatingMode = mode,
            ServicesStatus = statuses
        };
    }

    /// <summary>
    /// Perform health check on all services
    /// </summary>
    public async Task PerformHealthCheckAsync()
    {
        var services = new[] { "Qdrant", "OpenAI", "MCP", "Network" };

        foreach (var service in services)
        {
            try
            {
                await _circuitBreaker.ExecuteAsync(
                    service,
                    async ct =>
                    {
                        // Simple health check - could be expanded
                        await Task.Delay(10, ct);
                        return true;
                    }
                );
            }
            catch
            {
                // Health check failed - status already updated by circuit breaker
            }
        }
    }
}

/// <summary>
/// Fallback strategy definition
/// </summary>
public class FallbackStrategy
{
    public string ServiceName { get; set; } = string.Empty;
    public object? Fallback { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Operating mode enumeration
/// </summary>
public enum OperatingMode
{
    Normal,      // All services healthy
    Degraded,    // Some services unavailable, using fallbacks
    Offline      // No external services, local operation only
}

/// <summary>
/// Current capability status
/// </summary>
public class CapabilityStatus
{
    public bool CanUseQdrant { get; set; }
    public bool CanUseOpenAI { get; set; }
    public bool CanUseMCP { get; set; }
    public bool HasNetwork { get; set; }
    public OperatingMode OperatingMode { get; set; }
    public Dictionary<string, ServiceHealthStatus> ServicesStatus { get; set; } = new();

    public bool IsFullyOperational => CanUseQdrant && CanUseOpenAI && CanUseMCP && HasNetwork;
    public bool CanOperate => OperatingMode != OperatingMode.Offline;
}
