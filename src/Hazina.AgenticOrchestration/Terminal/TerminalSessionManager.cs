using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
    /// Get sessions that can be recovered after a crash/restart.
    /// Scans persisted session files and identifies ones whose processes are no longer running.
    /// </summary>
    Task<IEnumerable<RecoverableSessionInfo>> GetRecoverableSessionsAsync();

    /// <summary>
    /// Restore a previously persisted session by creating a new terminal process
    /// and replaying the transcript to the connected SignalR clients.
    /// </summary>
    Task<ITerminalSession> RestoreSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Scan for recoverable sessions on startup and mark them appropriately.
    /// Called once during application startup.
    /// </summary>
    Task ScanForRecoverableSessionsAsync();

    /// <summary>
    /// Archive a recoverable session (remove from active, move to archive).
    /// Use when the user decides not to restore a session.
    /// </summary>
    Task DismissRecoverableSessionAsync(string sessionId);
}

/// <summary>
/// Information about a session that can be recovered after a service restart.
/// </summary>
public class RecoverableSessionInfo
{
    public string SessionId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastActive { get; set; }
    public long TranscriptSizeBytes { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Implementation of terminal session manager with SignalR integration.
/// Uses ConPTY (Windows Pseudo Console) for full interactive terminal support.
/// Persists session metadata and transcripts for crash recovery.
/// </summary>
public class TerminalSessionManager : ITerminalSessionManager, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ITerminalSession> _sessions = new();
    private readonly IHubContext<TerminalHub> _hubContext;
    private readonly ILogger<TerminalSessionManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IAgentSessionLogger _sessionLogger;
    private readonly ISessionPersistence _persistence;
    private readonly Timer _cleanupTimer;
    private readonly bool _useConPty;
    private bool _disposed;

    public int SessionCount => _sessions.Count;

    public TerminalSessionManager(
        IHubContext<TerminalHub> hubContext,
        ILogger<TerminalSessionManager> logger,
        ILoggerFactory loggerFactory,
        IAgentSessionLogger sessionLogger,
        ISessionPersistence persistence)
    {
        _hubContext = hubContext;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _sessionLogger = sessionLogger;
        _persistence = persistence;

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

        // Persist session metadata for crash recovery
        var metadata = new SessionMetadata
        {
            SessionId = sessionId,
            CreatedAt = DateTime.UtcNow,
            LastActive = DateTime.UtcNow,
            Command = config.Command,
            WorkingDirectory = config.WorkingDirectory ?? string.Empty,
            Dimensions = new TerminalDimensions { Cols = config.Columns, Rows = config.Rows },
            State = SessionState.Active,
            DisplayOrder = _sessions.Count
        };
        await _persistence.SaveSessionAsync(sessionId, metadata);

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

                // Persist output for crash recovery
                var text = Encoding.UTF8.GetString(data);
                await _persistence.AppendOutputAsync(sessionId, text);

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

                // Archive the persisted session (move from active to archive)
                try
                {
                    await _persistence.ArchiveSessionAsync(sessionId);
                }
                catch (Exception archiveEx)
                {
                    _logger.LogWarning(archiveEx, "Failed to archive session {SessionId} on exit", sessionId);
                }

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

            // Archive the persisted session
            try
            {
                await _persistence.ArchiveSessionAsync(sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to archive session {SessionId} on removal", sessionId);
            }

            _logger.LogInformation("Removed terminal session {SessionId}, remaining: {Count}",
                sessionId, _sessions.Count);
        }
    }

    public async Task ScanForRecoverableSessionsAsync()
    {
        try
        {
            var activeSessions = await _persistence.GetActiveSessionsAsync();
            var recoverableCount = 0;

            foreach (var session in activeSessions)
            {
                // Skip sessions that are already tracked in memory (shouldn't happen on fresh startup)
                if (_sessions.ContainsKey(session.SessionId))
                    continue;

                // Mark as recoverable - the original process is dead since we just restarted
                session.State = SessionState.Recoverable;
                session.LastActive = session.LastActive;
                await _persistence.SaveSessionAsync(session.SessionId, session);
                recoverableCount++;

                _logger.LogInformation(
                    "Found recoverable session {SessionId} (command: {Command}, created: {Created})",
                    session.SessionId, session.Command, session.CreatedAt);
            }

            if (recoverableCount > 0)
            {
                _logger.LogInformation("Startup scan found {Count} recoverable session(s)", recoverableCount);
            }
            else
            {
                _logger.LogInformation("Startup scan: no recoverable sessions found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning for recoverable sessions on startup");
        }
    }

    public async Task<IEnumerable<RecoverableSessionInfo>> GetRecoverableSessionsAsync()
    {
        var activeSessions = await _persistence.GetActiveSessionsAsync();
        var recoverable = new List<RecoverableSessionInfo>();

        foreach (var session in activeSessions)
        {
            // Only return sessions marked as recoverable (not active in-memory sessions)
            if (session.State != SessionState.Recoverable)
                continue;

            // Calculate transcript size
            var transcript = await _persistence.GetTranscriptAsync(session.SessionId);

            recoverable.Add(new RecoverableSessionInfo
            {
                SessionId = session.SessionId,
                Command = session.Command,
                WorkingDirectory = session.WorkingDirectory,
                CreatedAt = session.CreatedAt,
                LastActive = session.LastActive,
                TranscriptSizeBytes = Encoding.UTF8.GetByteCount(transcript),
                DisplayOrder = session.DisplayOrder
            });
        }

        return recoverable.OrderBy(r => r.DisplayOrder);
    }

    public async Task<ITerminalSession> RestoreSessionAsync(string sessionId, CancellationToken ct = default)
    {
        // Load the persisted session metadata
        var metadata = await _persistence.LoadSessionAsync(sessionId);
        if (metadata == null)
        {
            throw new InvalidOperationException($"No persisted session found for {sessionId}");
        }

        if (metadata.State != SessionState.Recoverable)
        {
            throw new InvalidOperationException($"Session {sessionId} is not in recoverable state (current: {metadata.State})");
        }

        // Load the transcript
        var transcript = await _persistence.GetTranscriptAsync(sessionId);

        // Create a new terminal session with the same configuration
        var config = new TerminalSessionConfig
        {
            Command = metadata.Command,
            WorkingDirectory = string.IsNullOrEmpty(metadata.WorkingDirectory) ? null : metadata.WorkingDirectory,
            Columns = metadata.Dimensions.Cols > 0 ? metadata.Dimensions.Cols : 120,
            Rows = metadata.Dimensions.Rows > 0 ? metadata.Dimensions.Rows : 30
        };

        _logger.LogInformation(
            "Restoring session {OriginalSessionId} with command '{Command}' ({TranscriptSize} bytes transcript)",
            sessionId, config.Command, transcript.Length);

        // Create the new session (this persists new metadata under a new ID)
        var newSession = await CreateSessionAsync(config, ct);

        // Archive the old recoverable session (clean up old files)
        await _persistence.ArchiveSessionAsync(sessionId);

        // Send the historical transcript to connected SignalR clients
        // This replays the visual output so users see what was on screen before the crash
        if (!string.IsNullOrEmpty(transcript))
        {
            var transcriptBytes = Encoding.UTF8.GetBytes(transcript);
            var intArray = transcriptBytes.Select(b => (int)b).ToArray();

            await _hubContext.Clients
                .Group($"terminal-{newSession.SessionId}")
                .SendAsync("OnOutput", newSession.SessionId, intArray, CancellationToken.None);

            _logger.LogInformation(
                "Replayed {ByteCount} bytes of transcript to restored session {SessionId}",
                transcriptBytes.Length, newSession.SessionId);
        }

        return newSession;
    }

    public async Task DismissRecoverableSessionAsync(string sessionId)
    {
        var metadata = await _persistence.LoadSessionAsync(sessionId);
        if (metadata == null)
        {
            _logger.LogWarning("Cannot dismiss session {SessionId}: not found in persistence", sessionId);
            return;
        }

        if (metadata.State != SessionState.Recoverable)
        {
            _logger.LogWarning("Cannot dismiss session {SessionId}: not in recoverable state (current: {State})",
                sessionId, metadata.State);
            return;
        }

        await _persistence.ArchiveSessionAsync(sessionId);
        _logger.LogInformation("Dismissed recoverable session {SessionId} (archived)", sessionId);
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

        // On graceful shutdown, archive all active sessions
        foreach (var kvp in _sessions)
        {
            try
            {
                await kvp.Value.TerminateAsync();
                await kvp.Value.DisposeAsync();
                await _persistence.ArchiveSessionAsync(kvp.Key);
            }
            catch { /* ignore */ }
        }

        _sessions.Clear();

        _logger.LogInformation("TerminalSessionManager disposed (sessions archived for clean shutdown)");
    }
}
