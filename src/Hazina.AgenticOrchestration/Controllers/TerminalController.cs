using System.Data.SQLite;
using System.Reflection;
using Microsoft.AspNetCore.Http;
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
                Title = s.Title,
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
            Title = session.Title,
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
                    Title = session.Title,
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
    /// Create a new terminal session and execute a prompt immediately.
    /// This is a convenience endpoint that combines session creation with prompt execution,
    /// similar to claude-terminal --message "prompt".
    ///
    /// Workflow:
    /// 1. Create session with provided config (or defaults)
    /// 2. Wait for session to be running
    /// 3. Wait for Claude CLI to initialize (2 seconds)
    /// 4. Send prompt + Enter to the session
    /// 5. Return session details (client can connect via SignalR to see output)
    /// </summary>
    [HttpPost("sessions/execute")]
    public async Task<ActionResult<TerminalSessionDto>> CreateSessionAndExecute(
        [FromBody] ExecutePromptRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { error = "Prompt is required" });
        }

        _logger.LogInformation("Creating terminal session with prompt execution: {Prompt}",
            request.Prompt.Length > 50 ? request.Prompt.Substring(0, 50) + "..." : request.Prompt);

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
            // Step 1: Create session
            var session = await _sessionManager.CreateSessionAsync(config, ct);
            _logger.LogInformation("Created terminal session {SessionId} for prompt execution", session.SessionId);

            // Step 2: Wait for session to be running (timeout 10 seconds)
            var startupTimeout = DateTime.UtcNow.AddSeconds(10);
            while (!session.IsRunning && DateTime.UtcNow < startupTimeout)
            {
                await Task.Delay(100, ct);
            }

            if (!session.IsRunning)
            {
                _logger.LogWarning("Session {SessionId} not running after 10 seconds", session.SessionId);
                return StatusCode(500, new
                {
                    error = "Session failed to start",
                    sessionId = session.SessionId
                });
            }

            // Step 3: Wait for Claude CLI to initialize (same as claude-terminal: 2000ms)
            var initDelay = request.InitializationDelayMs ?? 2000;
            _logger.LogDebug("Waiting {Delay}ms for Claude CLI initialization in session {SessionId}",
                initDelay, session.SessionId);
            await Task.Delay(initDelay, ct);

            // Step 4: Send prompt with Enter (carriage return in raw terminal mode)
            var promptWithEnter = request.Prompt + "\r";
            await session.WriteInputAsync(promptWithEnter, ct);

            _logger.LogInformation("Sent prompt to session {SessionId}: {Prompt}",
                session.SessionId, request.Prompt.Length > 50 ? request.Prompt.Substring(0, 50) + "..." : request.Prompt);

            // Step 5: Return session details
            return CreatedAtAction(
                nameof(GetSession),
                new { sessionId = session.SessionId },
                new TerminalSessionDto
                {
                    SessionId = session.SessionId,
                    Command = session.Command,
                    Title = session.Title,
                    StartedAt = session.StartedAt,
                    IsRunning = session.IsRunning,
                    WaitingForInput = session.WaitingForInput,
                    SignalRHubUrl = "/hubs/terminal",
                    Instructions = "Connect to SignalR hub and call JoinSession(sessionId) to receive output"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create session and execute prompt");
            return StatusCode(500, new { error = "Failed to execute prompt", message = ex.Message });
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
    /// Execute a prompt in an existing terminal session.
    /// This is a convenience endpoint that sends a prompt with automatic Enter submission.
    ///
    /// Difference from SendInput:
    /// - SendInput: Sends raw text without Enter (for interactive typing)
    /// - Execute: Sends prompt + Enter (for automated instruction execution)
    ///
    /// Use case: Remote agent sends instructions to an existing Claude session.
    /// </summary>
    [HttpPost("sessions/{sessionId}/execute")]
    public async Task<IActionResult> ExecutePromptInSession(
        string sessionId,
        [FromBody] ExecuteInSessionRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { error = "Prompt is required" });
        }

        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
        {
            return NotFound(new { error = "Session not found", sessionId });
        }

        if (!session.IsRunning)
        {
            return BadRequest(new { error = "Session is not running", sessionId });
        }

        _logger.LogInformation("Executing prompt in session {SessionId}: {Prompt}",
            sessionId, request.Prompt.Length > 50 ? request.Prompt.Substring(0, 50) + "..." : request.Prompt);

        try
        {
            // Send prompt with Enter (carriage return for raw terminal mode)
            var promptWithEnter = request.Prompt + "\r";
            await session.WriteInputAsync(promptWithEnter, ct);

            return Ok(new
            {
                message = "Prompt executed",
                sessionId,
                promptLength = request.Prompt.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute prompt in session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to execute prompt", message = ex.Message });
        }
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
    /// Get archived session logs with pagination
    /// </summary>
    [HttpGet("archive")]
    public ActionResult<ArchivedSessionsResponse> GetArchivedSessions(
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 50)
    {
        if (string.IsNullOrEmpty(_options.AgentSessionLogsPath) || !Directory.Exists(_options.AgentSessionLogsPath))
        {
            return Ok(new ArchivedSessionsResponse
            {
                Sessions = new List<ArchivedSessionDto>(),
                TotalCount = 0,
                Page = page,
                PageSize = pageSize,
                HasMore = false
            });
        }

        try
        {
            // Scan all log files recursively and parse metadata
            var logFiles = Directory.GetFiles(_options.AgentSessionLogsPath, "session-*.log", SearchOption.AllDirectories)
                .Select(path =>
                {
                    var fileInfo = new FileInfo(path);
                    var metadata = ParseLogFileMetadata(path);
                    return new ArchivedSessionDto
                    {
                        SessionId = metadata.sessionId,
                        Command = metadata.command,
                        WorkingDirectory = metadata.workingDirectory,
                        StartedAt = metadata.startedAt ?? fileInfo.CreationTime,
                        EndedAt = metadata.endedAt,
                        ExitCode = metadata.exitCode,
                        LogFilePath = path,
                        LogSizeBytes = fileInfo.Length
                    };
                })
                .OrderByDescending(s => s.StartedAt)
                .ToList();

            var totalCount = logFiles.Count;
            var pagedSessions = logFiles
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new ArchivedSessionsResponse
            {
                Sessions = pagedSessions,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                HasMore = (page + 1) * pageSize < totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list archived sessions");
            return StatusCode(500, new { error = "Failed to list archived sessions", message = ex.Message });
        }
    }

    /// <summary>
    /// Get log content for an archived session
    /// </summary>
    [HttpGet("archive/{sessionId}/log")]
    public ActionResult<SessionLogResponse> GetArchivedSessionLog(string sessionId)
    {
        if (string.IsNullOrEmpty(_options.AgentSessionLogsPath) || !Directory.Exists(_options.AgentSessionLogsPath))
        {
            return NotFound(new { error = "Session logs not configured" });
        }

        try
        {
            // Find the log file for this session
            var logFiles = Directory.GetFiles(_options.AgentSessionLogsPath, $"session-{sessionId}.log", SearchOption.AllDirectories);
            if (logFiles.Length == 0)
            {
                // Try a partial match
                logFiles = Directory.GetFiles(_options.AgentSessionLogsPath, $"session-*{sessionId}*.log", SearchOption.AllDirectories);
            }

            if (logFiles.Length == 0)
            {
                return NotFound(new { error = "Session log not found", sessionId });
            }

            var logPath = logFiles[0];
            // Use FileShare.ReadWrite to allow reading files that are being written to by another process
            using var fileStream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fileStream);
            var content = reader.ReadToEnd();

            return Ok(new SessionLogResponse
            {
                SessionId = sessionId,
                Content = content,
                TotalBytes = content.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read session log for {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to read session log", message = ex.Message });
        }
    }

    /// <summary>
    /// Parse metadata from log file header
    /// </summary>
    private (string sessionId, string command, string? workingDirectory, DateTime? startedAt, DateTime? endedAt, int? exitCode) ParseLogFileMetadata(string path)
    {
        var sessionId = Path.GetFileNameWithoutExtension(path).Replace("session-", "");
        var command = "claude";
        string? workingDirectory = null;
        DateTime? startedAt = null;
        DateTime? endedAt = null;
        int? exitCode = null;

        try
        {
            // Use FileShare.ReadWrite to allow reading files that are being written to by another process
            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fileStream);
            var lineCount = 0;
            string? line;

            while ((line = reader.ReadLine()) != null && lineCount < 20)
            {
                lineCount++;

                if (line.StartsWith("Command:"))
                {
                    command = line.Substring("Command:".Length).Trim();
                }
                else if (line.StartsWith("Working Directory:"))
                {
                    var wd = line.Substring("Working Directory:".Length).Trim();
                    workingDirectory = wd == "(default)" ? null : wd;
                }
                else if (line.StartsWith("Started:"))
                {
                    var dateStr = line.Substring("Started:".Length).Trim();
                    if (DateTime.TryParse(dateStr, out var dt))
                    {
                        startedAt = dt;
                    }
                }
            }

            // Read from end for exit info (seek backward if file is large)
            reader.BaseStream.Seek(0, SeekOrigin.End);
            var endPosition = reader.BaseStream.Position;

            // Read last 2KB for exit code
            var readStart = Math.Max(0, endPosition - 2048);
            reader.BaseStream.Seek(readStart, SeekOrigin.Begin);
            reader.DiscardBufferedData();

            var tail = reader.ReadToEnd();
            var tailLines = tail.Split('\n');

            foreach (var tailLine in tailLines)
            {
                var trimmed = tailLine.Trim();
                if (trimmed.StartsWith("Ended:"))
                {
                    var dateStr = trimmed.Substring("Ended:".Length).Trim();
                    if (DateTime.TryParse(dateStr, out var dt))
                    {
                        endedAt = dt;
                    }
                }
                else if (trimmed.StartsWith("Exit Code:"))
                {
                    var codeStr = trimmed.Substring("Exit Code:".Length).Trim();
                    if (int.TryParse(codeStr, out var code))
                    {
                        exitCode = code;
                    }
                }
            }
        }
        catch
        {
            // If parsing fails, use defaults
        }

        return (sessionId, command, workingDirectory, startedAt, endedAt, exitCode);
    }

    /// <summary>
    /// Restore an archived session - creates a new session and sends the archived log content as input
    /// </summary>
    [HttpPost("archive/{sessionId}/restore")]
    public async Task<ActionResult<TerminalSessionDto>> RestoreArchivedSession(
        string sessionId,
        [FromQuery] bool includeTimestamps = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_options.AgentSessionLogsPath) || !Directory.Exists(_options.AgentSessionLogsPath))
        {
            return NotFound(new { error = "Session logs not configured" });
        }

        try
        {
            // Find the log file for this session
            var logFiles = Directory.GetFiles(_options.AgentSessionLogsPath, $"session-{sessionId}.log", SearchOption.AllDirectories);
            if (logFiles.Length == 0)
            {
                // Try a partial match
                logFiles = Directory.GetFiles(_options.AgentSessionLogsPath, $"session-*{sessionId}*.log", SearchOption.AllDirectories);
            }

            if (logFiles.Length == 0)
            {
                return NotFound(new { error = "Session log not found", sessionId });
            }

            var logPath = logFiles[0];

            // Read the log content
            using var fileStream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fileStream);
            var fullContent = await reader.ReadToEndAsync();

            // Extract just the conversation (remove timestamps and metadata if requested)
            var sessionContent = includeTimestamps ? fullContent : ExtractConversationContent(fullContent);

            // Create new session with the archived content as context
            var config = new TerminalSessionConfig
            {
                Command = _options.DefaultCommand,
                Arguments = _options.DefaultArguments,
                WorkingDirectory = _options.DefaultWorkingDirectory,
                Columns = _options.DefaultTerminalColumns,
                Rows = _options.DefaultTerminalRows
            };

            var newSession = await _sessionManager.CreateSessionAsync(config, ct);

            _logger.LogInformation("Created restore session {NewSessionId} from archived session {OriginalSessionId}",
                newSession.SessionId, sessionId);

            // Wait for session to be running (with timeout)
            var timeout = DateTime.UtcNow.AddSeconds(10);
            while (!newSession.IsRunning && DateTime.UtcNow < timeout)
            {
                await Task.Delay(100, ct);
            }

            if (!newSession.IsRunning)
            {
                _logger.LogWarning("Session {SessionId} not running after 10 seconds", newSession.SessionId);
                return StatusCode(500, new { error = "Session failed to start", sessionId = newSession.SessionId });
            }

            // Wait a bit more for Claude to fully initialize
            await Task.Delay(2000, ct);

            // Send the archived content to Claude as context
            var instruction = $"This is a restored session. Here is the previous conversation:\n\n{sessionContent}\n\nYou can now continue the conversation.";
            await newSession.WriteInputAsync(instruction + "\r", ct);

            _logger.LogInformation("Sent {ContentSize} bytes of context to restored session {SessionId}",
                sessionContent.Length, newSession.SessionId);

            // Return the new session with the content to display
            // Client will show content in terminal and Claude has the context
            return Ok(new
            {
                sessionId = newSession.SessionId,
                command = newSession.Command,
                title = $"Restored: {sessionId}",
                startedAt = newSession.StartedAt,
                isRunning = newSession.IsRunning,
                waitingForInput = newSession.WaitingForInput,
                signalRHubUrl = "/hubs/terminal",
                // Include the archived content for client to display
                archivedContent = sessionContent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to restore session", message = ex.Message });
        }
    }

    /// <summary>
    /// Extract conversation content from log file (remove timestamps, ANSI codes, and system metadata)
    /// </summary>
    private string ExtractConversationContent(string logContent)
    {
        var lines = logContent.Split('\n');
        var result = new System.Text.StringBuilder();
        var inHeader = true;
        var inFooter = false;
        var lastLineWasEmpty = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip header (everything before first output)
            if (inHeader)
            {
                if (trimmed.StartsWith("[") && trimmed.Contains("[OUT]"))
                {
                    inHeader = false;
                }
                else
                {
                    continue;
                }
            }

            // Skip footer (SESSION ENDED block)
            if (trimmed.Contains("SESSION ENDED"))
            {
                inFooter = true;
                continue;
            }

            if (inFooter)
            {
                continue;
            }

            // Remove timestamps from lines like "[12:34:56.789] [OUT] text"
            if (trimmed.StartsWith("[") && trimmed.Contains("]"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^\[\d{2}:\d{2}:\d{2}\.\d{3}\] \[(OUT|INPUT)\] (.*)$");
                if (match.Success)
                {
                    var type = match.Groups[1].Value;
                    var content = match.Groups[2].Value;

                    // Remove ANSI escape sequences
                    content = StripAnsiCodes(content);

                    if (type == "INPUT")
                    {
                        // Clean up input markers like [CR], [LF]
                        content = content.Replace("[CR]", "").Replace("[LF]", "\n").Replace(">>>", "").Trim();
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            result.AppendLine($"User: {content}");
                            lastLineWasEmpty = false;
                        }
                    }
                    else
                    {
                        // Collapse multiple empty lines into one
                        if (string.IsNullOrWhiteSpace(content))
                        {
                            if (!lastLineWasEmpty)
                            {
                                result.AppendLine();
                                lastLineWasEmpty = true;
                            }
                        }
                        else
                        {
                            result.AppendLine(content);
                            lastLineWasEmpty = false;
                        }
                    }
                }
                else
                {
                    // Strip ANSI from non-log lines too
                    var cleaned = StripAnsiCodes(trimmed);
                    if (!string.IsNullOrWhiteSpace(cleaned))
                    {
                        result.AppendLine(cleaned);
                        lastLineWasEmpty = false;
                    }
                    else if (!lastLineWasEmpty)
                    {
                        result.AppendLine();
                        lastLineWasEmpty = true;
                    }
                }
            }
            else
            {
                // Strip ANSI from all lines
                var cleaned = StripAnsiCodes(line);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    result.AppendLine(cleaned);
                    lastLineWasEmpty = false;
                }
                else if (!lastLineWasEmpty)
                {
                    result.AppendLine();
                    lastLineWasEmpty = true;
                }
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Remove ANSI escape sequences from text
    /// </summary>
    private static string StripAnsiCodes(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Remove ANSI CSI sequences (ESC [ ... m)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\x1B\[[0-9;]*m", "");

        // Remove other ANSI escape sequences (ESC followed by any character and parameters)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\x1B\[[\d;]*[A-Za-z]", "");

        // Remove OSC sequences (ESC ] ... BEL or ESC ] ... ST)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\x1B\][^\x07]*\x07", "");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\x1B\][^\x1B]*\x1B\\", "");

        // Remove other escape sequences
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\x1B[@-_][0-?]*[ -/]*[@-~]", "");

        return text;
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
                Title = s.Title,
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

    /// <summary>
    /// Get all recoverable sessions (crashed but persisted)
    /// </summary>
    [HttpGet("recoverable")]
    public async Task<ActionResult<IEnumerable<RecoverableSessionDto>>> GetRecoverableSessions()
    {
        try
        {
            var recoverable = await _sessionManager.GetRecoverableSessionsAsync();

            var dtos = recoverable.Select(m => new RecoverableSessionDto
            {
                SessionId = m.SessionId,
                Command = m.Command,
                WorkingDirectory = m.WorkingDirectory,
                CreatedAt = m.CreatedAt,
                LastActive = m.LastActive,
                State = m.State.ToString(),
                DisplayOrder = m.DisplayOrder
            });

            _logger.LogInformation("Returning {Count} recoverable sessions", dtos.Count());
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recoverable sessions");
            return StatusCode(500, new { error = "Failed to get recoverable sessions", message = ex.Message });
        }
    }

    /// <summary>
    /// Restore a crashed session from persisted state
    /// </summary>
    [HttpPost("sessions/{sessionId}/restore")]
    public async Task<ActionResult<TerminalSessionDto>> RestoreSession(
        string sessionId,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Restoring session {SessionId}", sessionId);

            var session = await _sessionManager.RestoreSessionAsync(sessionId, ct);

            if (session == null)
            {
                return NotFound(new { error = "Session not found or cannot be restored", sessionId });
            }

            return Ok(new TerminalSessionDto
            {
                SessionId = session.SessionId,
                Command = session.Command,
                Title = session.Title ?? "Restored: " + session.Command,
                StartedAt = session.StartedAt,
                IsRunning = session.IsRunning,
                WaitingForInput = session.WaitingForInput,
                ExitCode = session.ExitCode,
                SignalRHubUrl = "/hubs/terminal",
                Instructions = "Session restored. Connect to SignalR hub and call JoinSession(sessionId) to view transcript"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to restore session", message = ex.Message });
        }
    }

    /// <summary>
    /// Get application version
    /// </summary>
    [HttpGet("version")]
    public ActionResult GetVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.0.0";
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return Ok(new
        {
            version = informationalVersion ?? version,
            assemblyVersion = version
        });
    }

    /// <summary>
    /// Upload a file to the server
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(100_000_000)] // 100MB hard limit
    public async Task<ActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided" });
        }

        var uploadsPath = _options.UploadsPath;
        if (string.IsNullOrEmpty(uploadsPath))
        {
            return StatusCode(500, new { error = "Uploads not configured" });
        }

        var maxSizeBytes = (long)_options.MaxUploadFileSizeMB * 1024 * 1024;
        if (file.Length > maxSizeBytes)
        {
            return BadRequest(new { error = $"File too large. Maximum size: {_options.MaxUploadFileSizeMB}MB" });
        }

        try
        {
            Directory.CreateDirectory(uploadsPath);

            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var guid = Guid.NewGuid().ToString("N")[..8];
            var safeFileName = Path.GetFileName(file.FileName); // Strip path components
            var uniqueName = $"{timestamp}_{guid}_{safeFileName}";
            var filePath = Path.Combine(uploadsPath, uniqueName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("File uploaded: {FileName} ({Size} bytes) -> {Path}",
                safeFileName, file.Length, filePath);

            return Ok(new UploadResponseDto
            {
                FileName = uniqueName,
                OriginalName = safeFileName,
                Size = file.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file {FileName}", file.FileName);
            return StatusCode(500, new { error = "Failed to upload file" });
        }
    }

    /// <summary>
    /// List uploaded files
    /// </summary>
    [HttpGet("uploads")]
    public ActionResult GetUploadedFiles()
    {
        var uploadsPath = _options.UploadsPath;
        if (string.IsNullOrEmpty(uploadsPath) || !Directory.Exists(uploadsPath))
        {
            return Ok(new { files = Array.Empty<object>(), totalCount = 0 });
        }

        try
        {
            var files = Directory.GetFiles(uploadsPath)
                .Select(path =>
                {
                    var fileInfo = new FileInfo(path);
                    return new UploadedFileDto
                    {
                        FileName = fileInfo.Name,
                        Size = fileInfo.Length,
                        UploadedAt = fileInfo.CreationTime
                    };
                })
                .OrderByDescending(f => f.UploadedAt)
                .ToList();

            return Ok(new { files, totalCount = files.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list uploaded files");
            return StatusCode(500, new { error = "Failed to list files" });
        }
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

/// <summary>
/// Request to create a session and execute a prompt immediately.
/// Combines CreateTerminalSessionRequest with a prompt to execute.
/// </summary>
public class ExecutePromptRequest
{
    /// <summary>The prompt to execute in the new session (required)</summary>
    public string Prompt { get; set; } = "";

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

    /// <summary>Delay in milliseconds to wait for CLI initialization before sending prompt (default: 2000)</summary>
    public int? InitializationDelayMs { get; set; }
}

public class SendInputRequest
{
    /// <summary>Text input</summary>
    public string? Text { get; set; }

    /// <summary>Raw bytes input (base64 encoded)</summary>
    public byte[]? Bytes { get; set; }
}

/// <summary>
/// Request to execute a prompt in an existing session.
/// Automatically appends Enter to submit the prompt.
/// </summary>
public class ExecuteInSessionRequest
{
    /// <summary>The prompt to execute (required)</summary>
    public string Prompt { get; set; } = "";
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
    /// <summary>
    /// Dynamic title extracted from terminal output (e.g., from "STATUS: Title" pattern).
    /// If null, use Command as the display title.
    /// </summary>
    public string? Title { get; set; }
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

/// <summary>
/// Archived session info (from log file)
/// </summary>
public class ArchivedSessionDto
{
    public string SessionId { get; set; } = "";
    public string Command { get; set; } = "";
    public string? WorkingDirectory { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? ExitCode { get; set; }
    public string LogFilePath { get; set; } = "";
    public long LogSizeBytes { get; set; }
}

/// <summary>
/// Response for archived sessions list
/// </summary>
public class ArchivedSessionsResponse
{
    public List<ArchivedSessionDto> Sessions { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}

/// <summary>
/// Response for session log content
/// </summary>
public class SessionLogResponse
{
    public string SessionId { get; set; } = "";
    public string Content { get; set; } = "";
    public long TotalBytes { get; set; }
}

public class UploadResponseDto
{
    public string FileName { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public long Size { get; set; }
}

public class UploadedFileDto
{
    public string FileName { get; set; } = "";
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class RecoverableSessionDto
{
    public string SessionId { get; set; } = "";
    public string Command { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime LastActive { get; set; }
    public string State { get; set; } = "";
    public int DisplayOrder { get; set; }
}
