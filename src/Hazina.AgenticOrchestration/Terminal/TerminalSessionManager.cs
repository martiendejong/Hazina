using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Hazina.AgenticOrchestration.Hubs;
using Hazina.AgenticOrchestration.Services;
using Hazina.AgenticOrchestration.Terminal.ConPty;

namespace Hazina.AgenticOrchestration.Terminal;

/// <summary>
/// Manages multiple terminal sessions.
/// Thread-safe for concurrent access from SignalR hub.
/// </summary>
public interface ITerminalSessionManager
{
    /// <summary>
    /// Create and start a new terminal session
    /// </summary>
    Task<ITerminalSession> CreateSessionAsync(TerminalSessionConfig config, CancellationToken ct = default);

    /// <summary>
    /// Get an existing session by ID
    /// </summary>
    ITerminalSession? GetSession(string sessionId);

    /// <summary>
    /// Get all active sessions
    /// </summary>
    IEnumerable<ITerminalSession> GetAllSessions();

    /// <summary>
    /// Terminate and remove a session
    /// </summary>
    Task RemoveSessionAsync(string sessionId);

    /// <summary>
    /// Get session count
    /// </summary>
    int SessionCount { get; }

    /// <summary>
    /// Restore a crashed session from persisted state
    /// </summary>
    Task<ITerminalSession?> RestoreSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Get all recoverable sessions (crashed but persisted)
    /// </summary>
    Task<IEnumerable<SessionMetadata>> GetRecoverableSessionsAsync();
}

/// <summary>
/// Implementation of terminal session manager with SignalR integration.
/// Uses ConPTY (Windows Pseudo Console) for full interactive terminal support.
/// </summary>
public class TerminalSessionManager : ITerminalSessionManager, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ITerminalSession> _sessions = new();
    private readonly IHubContext<TerminalHub> _hubContext;
    private readonly ILogger<TerminalSessionManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IAgentSessionLogger _sessionLogger;
    private readonly ISessionPersistence _sessionPersistence;
    private readonly Timer _cleanupTimer;
    private readonly bool _useConPty;
    private bool _disposed;

    public int SessionCount => _sessions.Count;

    public TerminalSessionManager(
        IHubContext<TerminalHub> hubContext,
        ILogger<TerminalSessionManager> logger,
        ILoggerFactory loggerFactory,
        IAgentSessionLogger sessionLogger,
        ISessionPersistence sessionPersistence)
    {
        _hubContext = hubContext;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _sessionLogger = sessionLogger;
        _sessionPersistence = sessionPersistence;

        // Use ConPTY on Windows for full interactive terminal support
        _useConPty = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        _logger.LogInformation("Terminal session manager initialized. ConPTY enabled: {UseConPty}", _useConPty);

        // Cleanup dead sessions every 30 seconds
        _cleanupTimer = new Timer(CleanupDeadSessions, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task<ITerminalSession> CreateSessionAsync(TerminalSessionConfig config, CancellationToken ct = default)
    {
        var sessionId = GenerateSessionId();

        // Create appropriate session type based on platform
        ITerminalSession session;
        if (_useConPty)
        {
            var conPtyLogger = _loggerFactory.CreateLogger<ConPtyTerminalSession>();
            session = new ConPtyTerminalSession(sessionId, config, conPtyLogger);
            _logger.LogInformation("Creating ConPTY session {SessionId} for command '{Command}'", sessionId, config.Command);
        }
        else
        {
            var pipeLogger = _loggerFactory.CreateLogger<TerminalSession>();
            session = new TerminalSession(sessionId, config, pipeLogger);
            _logger.LogInformation("Creating pipe-based session {SessionId} for command '{Command}'", sessionId, config.Command);
        }

        // Start session logging
        await _sessionLogger.StartSessionAsync(sessionId, config.Command, config.WorkingDirectory);

        // Track last state to detect changes
        bool lastWaitingState = false;
        DateTime lastStateChangeSent = DateTime.MinValue;
        const int STATE_CHANGE_DEBOUNCE_MS = 500; // Debounce OnStateChanged events to prevent focus loss

        // NOTE: Periodic state check timer is DISABLED to prevent refresh flickering.
        // The WaitingForInput property is computed dynamically (checks idle time),
        // which causes it to flip from false->true when idle time crosses 1000ms.
        // This creates a race condition with the 2-second polling interval.
        // State changes are already detected immediately on output (lines 142-150).
        //
        // var stateCheckTimer = new System.Threading.Timer(async _ =>
        // {
        //     try
        //     {
        //         var currentWaitingState = session.WaitingForInput;
        //         if (currentWaitingState != lastWaitingState)
        //         {
        //             lastWaitingState = currentWaitingState;
        //             await _hubContext.Clients
        //                 .Group($"terminal-{sessionId}")
        //                 .SendAsync("OnStateChanged", sessionId, session.IsRunning, currentWaitingState, CancellationToken.None);
        //             _logger.LogDebug("State changed for session {SessionId}: WaitingForInput={Waiting}", sessionId, currentWaitingState);
        //         }
        //     }
        //     catch { /* ignore timer errors */ }
        // }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

        // Wire up output to SignalR
        // IMPORTANT: Don't use the request's cancellation token here!
        // The event handlers run after the HTTP request completes, so ct would be cancelled.
        session.OnOutput += async (data) =>
        {
            try
            {
                _logger.LogDebug("OUTPUT: Sending {ByteCount} bytes to session {SessionId}", data.Length, sessionId);

                // Log output to file
                await _sessionLogger.LogOutputAsync(sessionId, data);

                // Convert byte[] to int[] because System.Text.Json serializes byte[] as Base64 string
                // but the JavaScript client expects a number array
                var intArray = data.Select(b => (int)b).ToArray();
                await _hubContext.Clients
                    .Group($"terminal-{sessionId}")
                    .SendAsync("OnOutput", sessionId, intArray, CancellationToken.None);

                // Check state on output, but DEBOUNCE to prevent focus loss during rapid output
                // Problem: When Claude outputs character-by-character, WaitingForInput flips constantly
                // This triggers OnStateChanged → React re-render → user loses focus while typing
                // Solution: Only send OnStateChanged if 500ms passed since last state change event
                var currentWaitingState = session.WaitingForInput;
                var timeSinceLastChange = DateTime.UtcNow - lastStateChangeSent;

                if (currentWaitingState != lastWaitingState && timeSinceLastChange.TotalMilliseconds >= STATE_CHANGE_DEBOUNCE_MS)
                {
                    lastWaitingState = currentWaitingState;
                    lastStateChangeSent = DateTime.UtcNow;
                    await _hubContext.Clients
                        .Group($"terminal-{sessionId}")
                        .SendAsync("OnStateChanged", sessionId, session.IsRunning, currentWaitingState, CancellationToken.None);
                    _logger.LogDebug("State changed for session {SessionId}: WaitingForInput={Waiting}", sessionId, currentWaitingState);
                }
                else if (currentWaitingState != lastWaitingState)
                {
                    // State changed but we're in debounce period - just track it, don't send event
                    lastWaitingState = currentWaitingState;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending {ByteCount} bytes to SignalR for session {SessionId}",
                    data.Length, sessionId);
            }
        };

        session.OnExit += async (exitCode) =>
        {
            try
            {
                // NOTE: State check timer disposal removed (timer is disabled)

                // End session logging
                await _sessionLogger.EndSessionAsync(sessionId, exitCode);

                await _hubContext.Clients
                    .Group($"terminal-{sessionId}")
                    .SendAsync("OnExit", sessionId, exitCode, CancellationToken.None);

                // Also notify that session is no longer waiting (it's exited)
                await _hubContext.Clients
                    .Group($"terminal-{sessionId}")
                    .SendAsync("OnStateChanged", sessionId, false, false, CancellationToken.None);

                _logger.LogInformation("Session {SessionId} exited with code {ExitCode}", sessionId, exitCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending exit event to SignalR for session {SessionId}", sessionId);
            }
        };

        // Wire up title change to SignalR
        session.OnTitleChanged += async (title) =>
        {
            try
            {
                await _hubContext.Clients
                    .Group($"terminal-{sessionId}")
                    .SendAsync("OnTitleChanged", sessionId, title, CancellationToken.None);

                _logger.LogInformation("Session {SessionId} title changed to '{Title}'", sessionId, title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending title change to SignalR for session {SessionId}", sessionId);
            }
        };

        // Start the process
        await session.StartAsync(ct);

        if (!_sessions.TryAdd(sessionId, session))
        {
            await session.DisposeAsync();
            throw new InvalidOperationException($"Failed to add session {sessionId}");
        }

        _logger.LogInformation(
            "Created {SessionType} session {SessionId} running '{Command}', total sessions: {Count}",
            _useConPty ? "ConPTY" : "pipe-based", sessionId, config.Command, _sessions.Count);

        return session;
    }

    public ITerminalSession? GetSession(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var session) ? session : null;
    }

    public IEnumerable<ITerminalSession> GetAllSessions()
    {
        return _sessions.Values;
    }

    public async Task RemoveSessionAsync(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            await session.TerminateAsync();
            await session.DisposeAsync();

            _logger.LogInformation("Removed terminal session {SessionId}, remaining: {Count}",
                sessionId, _sessions.Count);
        }
    }

    private void CleanupDeadSessions(object? state)
    {
        // Only cleanup sessions that have been dead for more than 1 hour
        // This allows users to view historical output of closed sessions
        var cutoffTime = DateTime.UtcNow.AddHours(-1);

        var expiredSessions = _sessions
            .Where(kvp => !kvp.Value.IsRunning && kvp.Value.StartedAt < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionId in expiredSessions)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                _logger.LogInformation("Cleaned up expired session {SessionId} (dead > 1 hour)", sessionId);
                _ = session.DisposeAsync();
            }
        }

        if (expiredSessions.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired sessions, remaining: {Remaining}",
                expiredSessions.Count, _sessions.Count);
        }
    }

    public async Task<ITerminalSession?> RestoreSessionAsync(string sessionId, CancellationToken ct = default)
    {
        // Load persisted session metadata
        var metadata = await _sessionPersistence.LoadSessionAsync(sessionId);
        if (metadata == null)
        {
            _logger.LogWarning("No persisted metadata found for session {SessionId}", sessionId);
            return null;
        }

        // Check if session already exists (might have been restored already)
        if (_sessions.ContainsKey(sessionId))
        {
            _logger.LogInformation("Session {SessionId} already exists, returning existing session", sessionId);
            return _sessions[sessionId];
        }

        _logger.LogInformation("Restoring session {SessionId} from persisted state", sessionId);

        // Create a "replay-only" session (no actual process, just replay transcript)
        var config = new TerminalSessionConfig
        {
            Command = metadata.Command,
            WorkingDirectory = metadata.WorkingDirectory,
            Columns = metadata.Dimensions.Cols,
            Rows = metadata.Dimensions.Rows
        };

        // Create session but don't start the process
        ITerminalSession session;
        if (_useConPty)
        {
            var conPtyLogger = _loggerFactory.CreateLogger<ConPtyTerminalSession>();
            session = new ConPtyTerminalSession(sessionId, config, conPtyLogger);
        }
        else
        {
            var pipeLogger = _loggerFactory.CreateLogger<TerminalSession>();
            session = new TerminalSession(sessionId, config, pipeLogger);
        }

        // Wire up output to SignalR (same as CreateSessionAsync)
        session.OnOutput += async (data) =>
        {
            try
            {
                var intArray = data.Select(b => (int)b).ToArray();
                await _hubContext.Clients
                    .Group($"terminal-{sessionId}")
                    .SendAsync("OnOutput", sessionId, intArray, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending restored output to SignalR for session {SessionId}", sessionId);
            }
        };

        session.OnExit += async (exitCode) =>
        {
            try
            {
                await _hubContext.Clients
                    .Group($"terminal-{sessionId}")
                    .SendAsync("OnExit", sessionId, exitCode, CancellationToken.None);

                await _hubContext.Clients
                    .Group($"terminal-{sessionId}")
                    .SendAsync("OnStateChanged", sessionId, false, false, CancellationToken.None);

                _logger.LogInformation("Restored session {SessionId} exited with code {ExitCode}", sessionId, exitCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending exit event for restored session {SessionId}", sessionId);
            }
        };

        session.OnTitleChanged += async (title) =>
        {
            try
            {
                await _hubContext.Clients
                    .Group($"terminal-{sessionId}")
                    .SendAsync("OnTitleChanged", sessionId, title, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending title change for restored session {SessionId}", sessionId);
            }
        };

        // Add to sessions dictionary
        if (!_sessions.TryAdd(sessionId, session))
        {
            await session.DisposeAsync();
            throw new InvalidOperationException($"Failed to add restored session {sessionId}");
        }

        // Load and replay transcript
        var transcript = await _sessionPersistence.GetTranscriptAsync(sessionId);
        if (!string.IsNullOrEmpty(transcript))
        {
            _logger.LogInformation("Replaying {Length} bytes of transcript for session {SessionId}",
                transcript.Length, sessionId);

            // Send transcript to SignalR clients
            var transcriptBytes = System.Text.Encoding.UTF8.GetBytes(transcript);
            var intArray = transcriptBytes.Select(b => (int)b).ToArray();
            await _hubContext.Clients
                .Group($"terminal-{sessionId}")
                .SendAsync("OnOutput", sessionId, intArray, ct);
        }

        // Update metadata to mark as restored
        metadata.State = SessionState.Active;
        metadata.LastActive = DateTime.UtcNow;
        await _sessionPersistence.SaveSessionAsync(sessionId, metadata);

        _logger.LogInformation("Successfully restored session {SessionId}", sessionId);
        return session;
    }

    public async Task<IEnumerable<SessionMetadata>> GetRecoverableSessionsAsync()
    {
        var activeSessions = await _sessionPersistence.GetActiveSessionsAsync();

        // Filter to only sessions that crashed (not currently in _sessions dictionary)
        var recoverableSessions = activeSessions
            .Where(metadata => !_sessions.ContainsKey(metadata.SessionId))
            .Where(metadata => metadata.State != SessionState.Completed)
            .ToList();

        _logger.LogInformation("Found {Count} recoverable sessions", recoverableSessions.Count);
        return recoverableSessions;
    }

    private string GenerateSessionId()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var random = Guid.NewGuid().ToString("N")[..8];
        return $"{timestamp}-{random}";
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        await _cleanupTimer.DisposeAsync();

        var tasks = _sessions.Values.Select(async session =>
        {
            try
            {
                await session.TerminateAsync();
                await session.DisposeAsync();
            }
            catch { /* ignore */ }
        });

        await Task.WhenAll(tasks);
        _sessions.Clear();

        _logger.LogInformation("TerminalSessionManager disposed");
    }
}
