using System.Data.SQLite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Hazina.AgenticOrchestration.Terminal;
using Hazina.AgenticOrchestration.Hubs;
using Hazina.AgenticOrchestration.Extensions;

namespace Hazina.AgenticOrchestration.Controllers;

/// <summary>
/// REST API for terminal session management.
/// Use this to create sessions via REST, then connect via SignalR for streaming.
///
/// Typical workflow:
/// 1. POST /api/terminal/sessions - Create a session, get sessionId
/// 2. Connect to SignalR hub at /hubs/terminal
/// 3. Call hub.JoinSession(sessionId) to receive output
/// 4. Call hub.SendInput(sessionId, data) to send input
/// </summary>
[ApiController]
[Route("api/terminal")]
public class TerminalController : ControllerBase
{
    private readonly ITerminalSessionManager _sessionManager;
    private readonly ILogger<TerminalController> _logger;
    private readonly AgenticOrchestrationOptions _options;

    public TerminalController(
        ITerminalSessionManager sessionManager,
        ILogger<TerminalController> logger,
        Microsoft.Extensions.Options.IOptions<AgenticOrchestrationOptions> options)
    {
        _sessionManager = sessionManager;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Get all active terminal sessions
    /// </summary>
    [HttpGet("sessions")]
    public ActionResult<IEnumerable<TerminalSessionDto>> GetSessions()
    {
        var sessions = _sessionManager.GetAllSessions()
            .Select(s => new TerminalSessionDto
            {
                SessionId = s.SessionId,
                Command = s.Command,
                StartedAt = s.StartedAt,
                IsRunning = s.IsRunning,
                WaitingForInput = s.WaitingForInput,
                ExitCode = s.ExitCode,
                Runtime = DateTime.UtcNow - s.StartedAt
            });

        return Ok(sessions);
    }

    /// <summary>
    /// Get a specific terminal session
    /// </summary>
    [HttpGet("sessions/{sessionId}")]
    public ActionResult<TerminalSessionDto> GetSession(string sessionId)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
        {
            return NotFound(new { error = "Session not found", sessionId });
        }

        return Ok(new TerminalSessionDto
        {
            SessionId = session.SessionId,
            Command = session.Command,
            StartedAt = session.StartedAt,
            IsRunning = session.IsRunning,
            WaitingForInput = session.WaitingForInput,
            ExitCode = session.ExitCode,
            Runtime = DateTime.UtcNow - session.StartedAt
        });
    }

    /// <summary>
    /// Create a new terminal session.
    /// After creation, connect via SignalR to /hubs/terminal and call JoinSession(sessionId).
    /// </summary>
    [HttpPost("sessions")]
    public async Task<ActionResult<TerminalSessionDto>> CreateSession(
        [FromBody] CreateTerminalSessionRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Creating terminal session: {Command} {Args}",
            request.Command, string.Join(" ", request.Arguments ?? Array.Empty<string>()));

        var config = new TerminalSessionConfig
        {
            Command = request.Command ?? _options.DefaultCommand,
            Arguments = request.Arguments ?? _options.DefaultArguments,
            WorkingDirectory = request.WorkingDirectory ?? _options.DefaultWorkingDirectory,
            Columns = request.Columns ?? _options.DefaultTerminalColumns,
            Rows = request.Rows ?? _options.DefaultTerminalRows,
            MergeStderr = request.MergeStderr ?? true
        };

        if (request.Environment != null)
        {
            foreach (var (key, value) in request.Environment)
            {
                config.Environment[key] = value;
            }
        }

        try
        {
            var session = await _sessionManager.CreateSessionAsync(config, ct);

            _logger.LogInformation("Created terminal session {SessionId}", session.SessionId);

            return CreatedAtAction(
                nameof(GetSession),
                new { sessionId = session.SessionId },
                new TerminalSessionDto
                {
                    SessionId = session.SessionId,
                    Command = session.Command,
                    StartedAt = session.StartedAt,
                    IsRunning = session.IsRunning,
                    WaitingForInput = session.WaitingForInput,
                    SignalRHubUrl = "/hubs/terminal",
                    Instructions = "Connect to SignalR hub and call JoinSession(sessionId) to receive output"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create terminal session");
            return StatusCode(500, new { error = "Failed to create session", message = ex.Message });
        }
    }

    /// <summary>
    /// Terminate a terminal session
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> TerminateSession(string sessionId)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
        {
            return NotFound(new { error = "Session not found", sessionId });
        }

        _logger.LogInformation("Terminating terminal session {SessionId}", sessionId);

        await _sessionManager.RemoveSessionAsync(sessionId);

        return Ok(new { message = "Session terminated", sessionId });
    }

    /// <summary>
    /// Send input to a terminal session (alternative to SignalR)
    /// Note: SignalR is preferred for low-latency bidirectional communication
    /// </summary>
    [HttpPost("sessions/{sessionId}/input")]
    public async Task<IActionResult> SendInput(
        string sessionId,
        [FromBody] SendInputRequest request,
        CancellationToken ct)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
        {
            return NotFound(new { error = "Session not found", sessionId });
        }

        if (!session.IsRunning)
        {
            return BadRequest(new { error = "Session is not running", sessionId });
        }

        if (!string.IsNullOrEmpty(request.Text))
        {
            await session.WriteInputAsync(request.Text, ct);
        }
        else if (request.Bytes != null && request.Bytes.Length > 0)
        {
            await session.WriteInputAsync(request.Bytes, ct);
        }
        else
        {
            return BadRequest(new { error = "Either 'text' or 'bytes' must be provided" });
        }

        return Ok(new { message = "Input sent", sessionId });
    }

    /// <summary>
    /// Get terminal output history for a session
    /// </summary>
    [HttpGet("sessions/{sessionId}/history")]
    public ActionResult GetSessionHistory(string sessionId)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
        {
            return NotFound(new { error = "Session not found", sessionId });
        }

        var history = session.GetOutputHistory();

        _logger.LogDebug("Returning {ByteCount} bytes of history for session {SessionId}",
            history.Length, sessionId);

        // Return as array of integers for JavaScript compatibility
        return Ok(new
        {
            sessionId,
            historyLength = history.Length,
            data = history.Select(b => (int)b).ToArray()
        });
    }

    /// <summary>
    /// Send a signal to the process (e.g., SIGINT for Ctrl+C)
    /// </summary>
    [HttpPost("sessions/{sessionId}/signal")]
    public async Task<IActionResult> SendSignal(
        string sessionId,
        [FromBody] SendSignalRequest request)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
        {
            return NotFound(new { error = "Session not found", sessionId });
        }

        var signal = (request.Signal?.ToLowerInvariant()) switch
        {
            "interrupt" or "sigint" or "ctrl+c" => TerminalSignal.Interrupt,
            "quit" or "sigquit" => TerminalSignal.Quit,
            "terminate" or "sigterm" => TerminalSignal.Terminate,
            "kill" or "sigkill" => TerminalSignal.Kill,
            _ => TerminalSignal.Interrupt
        };

        await session.SendSignalAsync(signal);

        return Ok(new { message = $"Signal {signal} sent", sessionId });
    }

    /// <summary>
    /// Get terminal stats
    /// </summary>
    [HttpGet("stats")]
    public ActionResult<TerminalStatsDto> GetStats()
    {
        var sessions = _sessionManager.GetAllSessions().ToList();

        return Ok(new TerminalStatsDto
        {
            TotalSessions = sessions.Count,
            RunningSessions = sessions.Count(s => s.IsRunning),
            CompletedSessions = sessions.Count(s => !s.IsRunning),
            SignalRHubUrl = "/hubs/terminal"
        });
    }

    /// <summary>
    /// Get terminal configuration defaults.
    /// Frontend can use this to populate session creation forms.
    /// </summary>
    [HttpGet("config")]
    public ActionResult<TerminalConfigDto> GetConfig()
    {
        return Ok(new TerminalConfigDto
        {
            DefaultCommand = _options.DefaultCommand,
            DefaultWorkingDirectory = _options.DefaultWorkingDirectory,
            DefaultArguments = _options.DefaultArguments,
            DefaultColumns = _options.DefaultTerminalColumns,
            DefaultRows = _options.DefaultTerminalRows,
            MaxConcurrentSessions = _options.MaxConcurrentSessions,
            SessionTimeoutMinutes = _options.SessionTimeoutMinutes,
            SignalRHubUrl = _options.TerminalHubPath
        });
    }

    /// <summary>
    /// Get external Claude instances running on this machine (from agent tracking database).
    /// These are Claude agents that were started outside of this orchestration tool.
    /// </summary>
    [HttpGet("external-instances")]
    public async Task<ActionResult<IEnumerable<ExternalClaudeInstanceDto>>> GetExternalInstances()
    {
        var instances = new List<ExternalClaudeInstanceDto>();

        if (string.IsNullOrEmpty(_options.DatabasePath) || !System.IO.File.Exists(_options.DatabasePath))
        {
            _logger.LogWarning("Agent database not found at {Path}", _options.DatabasePath);
            return Ok(instances);
        }

        try
        {
            using var conn = new SQLiteConnection($"Data Source={_options.DatabasePath}");
            await conn.OpenAsync();

            // Get active agents that have had a heartbeat in the last minute
            var cmd = new SQLiteCommand(@"
                SELECT
                    agent_id,
                    session_id,
                    started_at,
                    last_heartbeat,
                    status,
                    current_task,
                    worktree_seat
                FROM agents
                WHERE status = 'active'
                  AND datetime(last_heartbeat) > datetime('now', '-1 minute')
                ORDER BY last_heartbeat DESC
            ", conn);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                instances.Add(new ExternalClaudeInstanceDto
                {
                    AgentId = reader.GetString(0),
                    SessionId = reader.IsDBNull(1) ? null : reader.GetString(1),
                    StartedAt = DateTime.TryParse(reader.GetString(2), out var started) ? started : DateTime.MinValue,
                    LastHeartbeat = DateTime.TryParse(reader.GetString(3), out var heartbeat) ? heartbeat : DateTime.MinValue,
                    Status = reader.GetString(4),
                    CurrentTask = reader.IsDBNull(5) ? null : reader.GetString(5),
                    WorktreeSeat = reader.IsDBNull(6) ? null : reader.GetString(6),
                    IsExternal = true
                });
            }

            _logger.LogDebug("Found {Count} external Claude instances", instances.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query external instances from database");
        }

        return Ok(instances);
    }

    /// <summary>
    /// Get all sessions including external instances (combined view)
    /// </summary>
    [HttpGet("all-sessions")]
    public async Task<ActionResult<AllSessionsDto>> GetAllSessionsAndInstances()
    {
        // Get terminal sessions created by this tool
        var terminalSessions = _sessionManager.GetAllSessions()
            .Select(s => new TerminalSessionDto
            {
                SessionId = s.SessionId,
                Command = s.Command,
                StartedAt = s.StartedAt,
                IsRunning = s.IsRunning,
                WaitingForInput = s.WaitingForInput,
                ExitCode = s.ExitCode,
                Runtime = DateTime.UtcNow - s.StartedAt
            })
            .ToList();

        // Get external Claude instances
        var externalInstances = new List<ExternalClaudeInstanceDto>();

        if (!string.IsNullOrEmpty(_options.DatabasePath) && System.IO.File.Exists(_options.DatabasePath))
        {
            try
            {
                using var conn = new SQLiteConnection($"Data Source={_options.DatabasePath}");
                await conn.OpenAsync();

                var cmd = new SQLiteCommand(@"
                    SELECT
                        agent_id,
                        session_id,
                        started_at,
                        last_heartbeat,
                        status,
                        current_task,
                        worktree_seat
                    FROM agents
                    WHERE status = 'active'
                      AND datetime(last_heartbeat) > datetime('now', '-1 minute')
                    ORDER BY last_heartbeat DESC
                ", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    externalInstances.Add(new ExternalClaudeInstanceDto
                    {
                        AgentId = reader.GetString(0),
                        SessionId = reader.IsDBNull(1) ? null : reader.GetString(1),
                        StartedAt = DateTime.TryParse(reader.GetString(2), out var started) ? started : DateTime.MinValue,
                        LastHeartbeat = DateTime.TryParse(reader.GetString(3), out var heartbeat) ? heartbeat : DateTime.MinValue,
                        Status = reader.GetString(4),
                        CurrentTask = reader.IsDBNull(5) ? null : reader.GetString(5),
                        WorktreeSeat = reader.IsDBNull(6) ? null : reader.GetString(6),
                        IsExternal = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query external instances");
            }
        }

        return Ok(new AllSessionsDto
        {
            TerminalSessions = terminalSessions,
            ExternalInstances = externalInstances,
            TotalCount = terminalSessions.Count + externalInstances.Count
        });
    }
}

public class CreateTerminalSessionRequest
{
    /// <summary>Command to execute (default: "claude")</summary>
    public string? Command { get; set; }

    /// <summary>Command arguments</summary>
    public string[]? Arguments { get; set; }

    /// <summary>Working directory</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Terminal columns (default: 120)</summary>
    public int? Columns { get; set; }

    /// <summary>Terminal rows (default: 30)</summary>
    public int? Rows { get; set; }

    /// <summary>Merge stderr into stdout (default: true)</summary>
    public bool? MergeStderr { get; set; }

    /// <summary>Additional environment variables</summary>
    public Dictionary<string, string>? Environment { get; set; }
}

public class SendInputRequest
{
    /// <summary>Text input</summary>
    public string? Text { get; set; }

    /// <summary>Raw bytes input (base64 encoded)</summary>
    public byte[]? Bytes { get; set; }
}

public class SendSignalRequest
{
    /// <summary>Signal name: interrupt, quit, terminate, kill</summary>
    public string? Signal { get; set; }
}

public class TerminalSessionDto
{
    public string SessionId { get; set; } = "";
    public string Command { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public bool IsRunning { get; set; }
    public bool WaitingForInput { get; set; }
    public int? ExitCode { get; set; }
    public TimeSpan? Runtime { get; set; }
    public string? SignalRHubUrl { get; set; }
    public string? Instructions { get; set; }
}

public class TerminalStatsDto
{
    public int TotalSessions { get; set; }
    public int RunningSessions { get; set; }
    public int CompletedSessions { get; set; }
    public string SignalRHubUrl { get; set; } = "/hubs/terminal";
}

public class ExternalClaudeInstanceDto
{
    public string AgentId { get; set; } = "";
    public string? SessionId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public string Status { get; set; } = "";
    public string? CurrentTask { get; set; }
    public string? WorktreeSeat { get; set; }
    public bool IsExternal { get; set; } = true;
}

public class AllSessionsDto
{
    public List<TerminalSessionDto> TerminalSessions { get; set; } = new();
    public List<ExternalClaudeInstanceDto> ExternalInstances { get; set; } = new();
    public int TotalCount { get; set; }
}

/// <summary>
/// Terminal configuration DTO for exposing defaults to frontend
/// </summary>
public class TerminalConfigDto
{
    /// <summary>Default command/executable to run</summary>
    public string DefaultCommand { get; set; } = "claude";

    /// <summary>Default working directory (null means current directory)</summary>
    public string? DefaultWorkingDirectory { get; set; }

    /// <summary>Default arguments to pass to the command</summary>
    public string[] DefaultArguments { get; set; } = Array.Empty<string>();

    /// <summary>Default terminal columns</summary>
    public int DefaultColumns { get; set; } = 120;

    /// <summary>Default terminal rows</summary>
    public int DefaultRows { get; set; } = 30;

    /// <summary>Maximum concurrent sessions</summary>
    public int MaxConcurrentSessions { get; set; } = 10;

    /// <summary>Session timeout in minutes</summary>
    public int SessionTimeoutMinutes { get; set; } = 60;

    /// <summary>SignalR hub URL for terminal connections</summary>
    public string SignalRHubUrl { get; set; } = "/hubs/terminal";
}
