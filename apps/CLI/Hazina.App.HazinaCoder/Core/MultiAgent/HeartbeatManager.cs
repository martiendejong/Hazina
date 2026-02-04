using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.App.HazinaCoder.Core.MultiAgent;

/// <summary>
/// Heartbeat manager for agent liveness detection.
/// Sends periodic heartbeats and detects crashed agents.
///
/// Improvement #37: Heartbeat & Liveness Detection (Value: 9/10, Effort: 5/10, Ratio: 1.80)
/// </summary>
public class HeartbeatManager : IDisposable
{
    private readonly CoordinationDatabase _db;
    private readonly string _agentId;
    private readonly TimeSpan _heartbeatInterval;
    private readonly TimeSpan _crashTimeout;
    private Timer? _heartbeatTimer;
    private Timer? _crashDetectionTimer;
    private string? _currentWorkItem;

    /// <summary>
    /// Event fired when crashed agents are detected
    /// </summary>
    public event EventHandler<CrashedAgentsEventArgs>? CrashedAgentsDetected;

    public HeartbeatManager(
        CoordinationDatabase db,
        string agentId,
        TimeSpan? heartbeatInterval = null,
        TimeSpan? crashTimeout = null)
    {
        _db = db;
        _agentId = agentId;
        _heartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(10);
        _crashTimeout = crashTimeout ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Start heartbeat system
    /// </summary>
    public void Start()
    {
        Console.WriteLine($"💓 Starting heartbeat system (interval: {_heartbeatInterval.TotalSeconds}s, timeout: {_crashTimeout.TotalSeconds}s)");

        // Start heartbeat timer
        _heartbeatTimer = new Timer(
            async _ => await SendHeartbeatAsync(),
            null,
            TimeSpan.Zero,
            _heartbeatInterval);

        // Start crash detection timer
        _crashDetectionTimer = new Timer(
            async _ => await DetectCrashedAgentsAsync(),
            null,
            _crashTimeout,
            _crashTimeout);

        Console.WriteLine("   ✅ Heartbeat system started");
    }

    /// <summary>
    /// Stop heartbeat system
    /// </summary>
    public void Stop()
    {
        Console.WriteLine("💓 Stopping heartbeat system...");

        _heartbeatTimer?.Dispose();
        _crashDetectionTimer?.Dispose();

        Console.WriteLine("   ✅ Heartbeat system stopped");
    }

    /// <summary>
    /// Update current work item (included in heartbeat)
    /// </summary>
    public void UpdateWorkItem(string? workItem)
    {
        _currentWorkItem = workItem;
    }

    /// <summary>
    /// Send heartbeat to coordination database
    /// </summary>
    private async Task SendHeartbeatAsync()
    {
        try
        {
            await _db.UpdateHeartbeatAsync(_agentId, _currentWorkItem);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Heartbeat send failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Detect crashed agents (no heartbeat in timeout period)
    /// </summary>
    private async Task DetectCrashedAgentsAsync()
    {
        try
        {
            var crashedAgents = await _db.DetectCrashedAgentsAsync(_crashTimeout);

            if (crashedAgents.Any())
            {
                Console.WriteLine($"💀 Detected {crashedAgents.Count} crashed agent(s): {string.Join(", ", crashedAgents)}");

                // Fire event for crash handling
                CrashedAgentsDetected?.Invoke(this, new CrashedAgentsEventArgs
                {
                    CrashedAgentIds = crashedAgents,
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Crash detection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Manually trigger crash detection
    /// </summary>
    public async Task<List<string>> DetectCrashesNowAsync()
    {
        return await _db.DetectCrashedAgentsAsync(_crashTimeout);
    }

    public void Dispose()
    {
        Stop();
    }
}

/// <summary>
/// Event args for crashed agent detection
/// </summary>
public class CrashedAgentsEventArgs : EventArgs
{
    public List<string> CrashedAgentIds { get; set; } = new();
    public DateTime DetectedAt { get; set; }
}
