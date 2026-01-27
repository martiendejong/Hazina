using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Hazina.AgenticOrchestration.Hubs;

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
}

/// <summary>
/// Implementation of terminal session manager with SignalR integration.
/// </summary>
public class TerminalSessionManager : ITerminalSessionManager, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, TerminalSession> _sessions = new();
    private readonly IHubContext<TerminalHub> _hubContext;
    private readonly ILogger<TerminalSessionManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public int SessionCount => _sessions.Count;

    public TerminalSessionManager(
        IHubContext<TerminalHub> hubContext,
        ILogger<TerminalSessionManager> logger,
        ILoggerFactory loggerFactory)
    {
        _hubContext = hubContext;
        _logger = logger;
        _loggerFactory = loggerFactory;

        // Cleanup dead sessions every 30 seconds
        _cleanupTimer = new Timer(CleanupDeadSessions, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task<ITerminalSession> CreateSessionAsync(TerminalSessionConfig config, CancellationToken ct = default)
    {
        var sessionId = GenerateSessionId();
        var sessionLogger = _loggerFactory.CreateLogger<TerminalSession>();

        var session = new TerminalSession(sessionId, config, sessionLogger);

        // Wire up output to SignalR
        session.OnOutput += async (data) =>
        {
            try
            {
                await _hubContext.Clients
                    .Group($"terminal-{sessionId}")
                    .SendAsync("OnOutput", sessionId, data, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending output to SignalR for session {SessionId}", sessionId);
            }
        };

        session.OnExit += async (exitCode) =>
        {
            try
            {
                await _hubContext.Clients
                    .Group($"terminal-{sessionId}")
                    .SendAsync("OnExit", sessionId, exitCode, ct);

                _logger.LogInformation("Session {SessionId} exited with code {ExitCode}", sessionId, exitCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending exit event to SignalR for session {SessionId}", sessionId);
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
            "Created terminal session {SessionId} running '{Command}', total sessions: {Count}",
            sessionId, config.Command, _sessions.Count);

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
        var deadSessions = _sessions
            .Where(kvp => !kvp.Value.IsRunning)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionId in deadSessions)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                _logger.LogInformation("Cleaned up dead session {SessionId}", sessionId);
                _ = session.DisposeAsync();
            }
        }

        if (deadSessions.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} dead sessions, remaining: {Remaining}",
                deadSessions.Count, _sessions.Count);
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
