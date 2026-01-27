using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Hazina.AgenticOrchestration.Terminal;
using Hazina.AgenticOrchestration.Hubs;

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

    public TerminalController(
        ITerminalSessionManager sessionManager,
        ILogger<TerminalController> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
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
            Command = request.Command ?? "claude",
            Arguments = request.Arguments ?? Array.Empty<string>(),
            WorkingDirectory = request.WorkingDirectory,
            Columns = request.Columns ?? 120,
            Rows = request.Rows ?? 30,
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
