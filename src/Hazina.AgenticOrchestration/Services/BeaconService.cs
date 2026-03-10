using Hazina.AgenticOrchestration.Models;
using Hazina.AgenticOrchestration.Terminal;
using Hazina.AgenticOrchestration.Validation;
using Microsoft.Extensions.Logging;

namespace Hazina.AgenticOrchestration.Services;

/// <summary>
/// Service for building and sending structured startup beacons to Claude agents
/// Inspired by Overstory's buildBeacon() function
/// </summary>
public interface IBeaconService
{
    /// <summary>
    /// Build a structured startup beacon prompt
    /// </summary>
    string BuildBeacon(BeaconOptions options);

    /// <summary>
    /// Send a beacon to a terminal session with double-Enter workaround
    /// </summary>
    Task<BeaconResult> SendBeaconAsync(string sessionId, BeaconOptions options);
}

public class BeaconService : IBeaconService
{
    private readonly ITerminalSessionManager _terminalManager;
    private readonly ILogger<BeaconService> _logger;

    public BeaconService(ITerminalSessionManager terminalManager, ILogger<BeaconService> logger)
    {
        _terminalManager = terminalManager;
        _logger = logger;
    }

    public string BuildBeacon(BeaconOptions options)
    {
        // Validate inputs to prevent prompt injection
        if (!InputValidator.IsValidAgentName(options.AgentName))
        {
            throw new ArgumentException($"Invalid agent name: {options.AgentName}");
        }

        if (!InputValidator.IsValidTaskId(options.TaskId))
        {
            throw new ArgumentException($"Invalid task ID: {options.TaskId}");
        }

        if (!string.IsNullOrEmpty(options.ParentAgent) && !InputValidator.IsValidAgentName(options.ParentAgent))
        {
            throw new ArgumentException($"Invalid parent agent name: {options.ParentAgent}");
        }

        var timestamp = DateTime.UtcNow.ToString("O");
        var parent = options.ParentAgent ?? "none";

        // Escape agent name for shell command (defense in depth)
        var safeAgentName = InputValidator.EscapeShellArgument(options.AgentName);

        var protocol = options.CustomProtocol ??
            $"read .claude/CLAUDE.md, run mulch prime, check mail (hazina-orchestration mail-check --agent \"{safeAgentName}\"), then begin task {options.TaskId}";

        var parts = new List<string>
        {
            $"[HAZINA] {options.AgentName} ({options.Capability}) {timestamp} task:{options.TaskId}",
            $"Depth: {options.Depth} | Parent: {parent}",
            $"Startup: {protocol}"
        };

        return string.Join(" — ", parts);
    }

    public async Task<BeaconResult> SendBeaconAsync(string sessionId, BeaconOptions options)
    {
        try
        {
            var beacon = BuildBeacon(options);

            _logger.LogInformation("Sending beacon to session {SessionId}: {AgentName}", sessionId, options.AgentName);

            // Get the session
            var session = _terminalManager.GetSession(sessionId);
            if (session == null)
            {
                _logger.LogError("Session {SessionId} not found", sessionId);
                return new BeaconResult
                {
                    Success = false,
                    Error = $"Session {sessionId} not found",
                    SentAt = DateTime.UtcNow
                };
            }

            // Wait 3 seconds for Claude TUI to initialize (Overstory pattern)
            await Task.Delay(3000);

            // Send beacon text
            await session.WriteInputAsync(beacon);

            // Wait 500ms then send empty string (Enter) - double Enter workaround
            // Claude Code TUI sometimes consumes first Enter during re-render
            await Task.Delay(500);
            await session.WriteInputAsync("");

            _logger.LogInformation("Beacon sent successfully to {AgentName}", options.AgentName);

            return new BeaconResult
            {
                Success = true,
                SentAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send beacon to session {SessionId}", sessionId);
            return new BeaconResult
            {
                Success = false,
                Error = ex.Message,
                SentAt = DateTime.UtcNow
            };
        }
    }
}
