using System.Linq;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Hazina.AgenticOrchestration.Services;
using Hazina.AgenticOrchestration.Terminal;
using Hazina.AgenticOrchestration.Models;

namespace Hazina.AgenticOrchestration.Hubs;

/// <summary>
/// SignalR hub for real-time terminal communication.
///
/// Client Methods (called by server):
/// - OnOutput(sessionId, bytes[]) - Output from the process
/// - OnExit(sessionId, exitCode) - Process exited
/// - OnSessionCreated(sessionId, config) - New session created
/// - OnError(sessionId, error) - Error occurred
///
/// Server Methods (called by client):
/// - StartSession(config) -> sessionId - Create a new terminal session
/// - JoinSession(sessionId) - Subscribe to a session's output
/// - LeaveSession(sessionId) - Unsubscribe from a session
/// - SendInput(sessionId, bytes[]) - Send input to the process
/// - SendInputText(sessionId, text) - Send text input (convenience)
/// - Resize(sessionId, cols, rows) - Resize terminal
/// - SendSignal(sessionId, signal) - Send signal (e.g., SIGINT)
/// - Terminate(sessionId) - Terminate session
/// - GetSessions() - List all active sessions
/// </summary>
public class TerminalHub : Hub
{
    private readonly ITerminalSessionManager _sessionManager;
    private readonly ILogger<TerminalHub> _logger;
    private readonly IAgentSessionLogger _sessionLogger;
    private readonly IPromptTemplateService _promptTemplateService;

    public TerminalHub(
        ITerminalSessionManager sessionManager,
        ILogger<TerminalHub> logger,
        IAgentSessionLogger sessionLogger,
        IPromptTemplateService promptTemplateService)
    {
        _sessionManager = sessionManager;
        _logger = logger;
        _sessionLogger = sessionLogger;
        _promptTemplateService = promptTemplateService;
    }

    /// <summary>
    /// Create a new terminal session
    /// </summary>
    public async Task<TerminalSessionInfo> StartSession(TerminalStartRequest request)
    {
        _logger.LogInformation("Client {ConnectionId} starting session: {Command}",
            Context.ConnectionId, request.Command);

        var config = new TerminalSessionConfig
        {
            Command = request.Command ?? "claude",
            Arguments = request.Arguments ?? Array.Empty<string>(),
            WorkingDirectory = request.WorkingDirectory,
            Columns = request.Columns ?? 120,
            Rows = request.Rows ?? 30,
            MergeStderr = request.MergeStderr ?? true
        };

        // Add custom environment
        if (request.Environment != null)
        {
            foreach (var (key, value) in request.Environment)
            {
                config.Environment[key] = value;
            }
        }

        var session = await _sessionManager.CreateSessionAsync(config);

        // Auto-join the session
        await Groups.AddToGroupAsync(Context.ConnectionId, $"terminal-{session.SessionId}");

        _logger.LogInformation("Session {SessionId} created by client {ConnectionId}",
            session.SessionId, Context.ConnectionId);

        // Handle prompt template if provided
        PromptTemplate? template = null;
        if (!string.IsNullOrWhiteSpace(request.PromptTemplateId))
        {
            template = await _promptTemplateService.GetTemplateAsync(request.PromptTemplateId);
            if (template != null)
            {
                _logger.LogInformation("Session {SessionId} will auto-execute prompt template '{Name}' ({Id})",
                    session.SessionId, template.Name, template.Id);

                // Record template usage
                await _promptTemplateService.RecordTemplateUsageAsync(template.Id);

                // Schedule auto-execution after delay
                if (template.AutoExecute)
                {
                    _ = Task.Run(async () =>
                    {
                        // Wait for Claude to load (configurable delay)
                        await Task.Delay(template.AutoExecuteDelayMs);

                        // Send the prompt text + Enter key
                        var promptWithEnter = template.PromptText + "\n";
                        await session.WriteInputAsync(promptWithEnter);

                        _logger.LogInformation("Auto-executed prompt template '{Name}' in session {SessionId}",
                            template.Name, session.SessionId);
                    });
                }
            }
            else
            {
                _logger.LogWarning("Prompt template {TemplateId} not found for session {SessionId}",
                    request.PromptTemplateId, session.SessionId);
            }
        }

        // Notify all clients about new session (optional)
        await Clients.All.SendAsync("OnSessionCreated", new TerminalSessionInfo
        {
            SessionId = session.SessionId,
            Command = session.Command,
            StartedAt = session.StartedAt,
            IsRunning = session.IsRunning
        });

        return new TerminalSessionInfo
        {
            SessionId = session.SessionId,
            Command = session.Command,
            StartedAt = session.StartedAt,
            IsRunning = session.IsRunning
        };
    }

    /// <summary>
    /// Join an existing session to receive its output
    /// </summary>
    public async Task<bool> JoinSession(string sessionId)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
        {
            _logger.LogWarning("Client {ConnectionId} tried to join non-existent session {SessionId}",
                Context.ConnectionId, sessionId);
            return false;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"terminal-{sessionId}");

        _logger.LogInformation("Client {ConnectionId} joined session {SessionId}",
            Context.ConnectionId, sessionId);

        return true;
    }

    /// <summary>
    /// Leave a session (stop receiving output)
    /// </summary>
    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"terminal-{sessionId}");

        _logger.LogInformation("Client {ConnectionId} left session {SessionId}",
            Context.ConnectionId, sessionId);
    }

    /// <summary>
    /// Send input bytes to a session.
    /// Accepts int[] because JavaScript sends number arrays (System.Text.Json can't deserialize directly to byte[]).
    /// </summary>
    public async Task SendInput(string sessionId, int[] data)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
        {
            _logger.LogWarning("Cannot send input to non-existent session {SessionId}", sessionId);
            await Clients.Caller.SendAsync("OnError", sessionId, "Session not found");
            return;
        }

        // Convert int[] to byte[] (JavaScript sends numbers, not bytes)
        var bytes = data.Select(i => (byte)i).ToArray();

        // Log input to file
        await _sessionLogger.LogInputAsync(sessionId, bytes);

        await session.WriteInputAsync(bytes);
    }

    /// <summary>
    /// Send text input to a session (convenience method)
    /// </summary>
    public async Task SendInputText(string sessionId, string text)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
        {
            _logger.LogWarning("Cannot send input to non-existent session {SessionId}", sessionId);
            await Clients.Caller.SendAsync("OnError", sessionId, "Session not found");
            return;
        }

        // Log input to file
        await _sessionLogger.LogInputAsync(sessionId, text);

        await session.WriteInputAsync(text);
    }

    /// <summary>
    /// Resize the terminal
    /// </summary>
    public async Task Resize(string sessionId, int columns, int rows)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null) return;

        await session.ResizeAsync(columns, rows);

        _logger.LogDebug("Session {SessionId} resized to {Cols}x{Rows}",
            sessionId, columns, rows);
    }

    /// <summary>
    /// Send a signal to the process (e.g., SIGINT for Ctrl+C)
    /// </summary>
    public async Task SendSignal(string sessionId, string signalName)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null) return;

        var signal = signalName.ToLowerInvariant() switch
        {
            "interrupt" or "sigint" or "ctrl+c" => TerminalSignal.Interrupt,
            "quit" or "sigquit" => TerminalSignal.Quit,
            "terminate" or "sigterm" => TerminalSignal.Terminate,
            "kill" or "sigkill" => TerminalSignal.Kill,
            _ => TerminalSignal.Interrupt
        };

        await session.SendSignalAsync(signal);

        _logger.LogInformation("Sent {Signal} to session {SessionId}", signal, sessionId);
    }

    /// <summary>
    /// Terminate a session
    /// </summary>
    public async Task Terminate(string sessionId)
    {
        _logger.LogInformation("Client {ConnectionId} terminating session {SessionId}",
            Context.ConnectionId, sessionId);

        await _sessionManager.RemoveSessionAsync(sessionId);
    }

    /// <summary>
    /// Get all active sessions
    /// </summary>
    public IEnumerable<TerminalSessionInfo> GetSessions()
    {
        return _sessionManager.GetAllSessions().Select(s => new TerminalSessionInfo
        {
            SessionId = s.SessionId,
            Command = s.Command,
            StartedAt = s.StartedAt,
            IsRunning = s.IsRunning,
            ExitCode = s.ExitCode
        });
    }

    /// <summary>
    /// Get a specific session's info
    /// </summary>
    public TerminalSessionInfo? GetSession(string sessionId)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null) return null;

        return new TerminalSessionInfo
        {
            SessionId = session.SessionId,
            Command = session.Command,
            StartedAt = session.StartedAt,
            IsRunning = session.IsRunning,
            ExitCode = session.ExitCode
        };
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Request to start a terminal session
/// </summary>
public class TerminalStartRequest
{
    public string? Command { get; set; }
    public string[]? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public int? Columns { get; set; }
    public int? Rows { get; set; }
    public bool? MergeStderr { get; set; }
    public Dictionary<string, string>? Environment { get; set; }

    /// <summary>
    /// Optional prompt template ID to auto-execute after session loads
    /// </summary>
    public string? PromptTemplateId { get; set; }
}

/// <summary>
/// Information about a terminal session
/// </summary>
public class TerminalSessionInfo
{
    public string SessionId { get; set; } = "";
    public string Command { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public bool IsRunning { get; set; }
    public int? ExitCode { get; set; }
}
