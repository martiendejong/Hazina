using Hazina.AgenticOrchestration.Abstractions;
using System.Text;

namespace Hazina.AgenticOrchestration.Services;

/// <summary>
/// Logs all input and output from agent terminal sessions to files.
/// Files are organized by date/hour to prevent too many files in one directory.
/// </summary>
public interface IAgentSessionLogger
{
    /// <summary>
    /// Start logging for a session. Creates the log file.
    /// </summary>
    /// <param name="sessionId">Unique session identifier</param>
    /// <param name="command">The command being executed</param>
    /// <param name="workingDirectory">Working directory of the session</param>
    Task StartSessionAsync(string sessionId, string command, string? workingDirectory);

    /// <summary>
    /// Log output bytes from the terminal
    /// </summary>
    Task LogOutputAsync(string sessionId, byte[] data);

    /// <summary>
    /// Log output text from the terminal
    /// </summary>
    Task LogOutputAsync(string sessionId, string text);

    /// <summary>
    /// Log input bytes sent to the terminal
    /// </summary>
    Task LogInputAsync(string sessionId, byte[] data);

    /// <summary>
    /// Log input text sent to the terminal
    /// </summary>
    Task LogInputAsync(string sessionId, string text);

    /// <summary>
    /// End logging for a session. Writes final entry and closes the file.
    /// </summary>
    Task EndSessionAsync(string sessionId, int? exitCode);

    /// <summary>
    /// Get the log file path for a session
    /// </summary>
    string? GetLogFilePath(string sessionId);
}

/// <summary>
/// Implementation of agent session logger that writes to files organized by date/hour.
///
/// Directory structure:
///   {BasePath}/{yyyy-MM-dd}/{HH}/session-{sessionId}.log
///
/// Example:
///   C:\scripts\logs\agent-sessions\2026-01-28\14\session-20260128-143052-abc12345.log
/// </summary>
public class AgentSessionLogger : IAgentSessionLogger, IAsyncDisposable
{
    private readonly string _basePath;
    private readonly Dictionary<string, SessionLogContext> _sessions = new();
    private readonly object _lock = new();
    private readonly IFileSystem _fileSystem;
    private readonly ISystemClock _clock;
    private bool _disposed;

    public AgentSessionLogger(string basePath, IFileSystem fileSystem, ISystemClock clock)
    {
        _basePath = basePath;
        _fileSystem = fileSystem;
        _clock = clock;

        // Ensure base directory exists
        _fileSystem.CreateDirectory(_basePath);
    }

    public Task StartSessionAsync(string sessionId, string command, string? workingDirectory)
    {
        if (_disposed) return Task.CompletedTask;

        lock (_lock)
        {
            if (_sessions.ContainsKey(sessionId))
                return Task.CompletedTask;

            var now = _clock.Now;
            var context = new SessionLogContext
            {
                SessionId = sessionId,
                StartTime = now,
                FilePath = BuildFilePath(sessionId, now)
            };

            // Ensure directory exists
            var directory = Path.GetDirectoryName(context.FilePath);
            if (directory != null && !_fileSystem.DirectoryExists(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }

            // Create file and write header
            context.Writer = new StreamWriter(context.FilePath, append: false, Encoding.UTF8)
            {
                AutoFlush = true
            };

            context.Writer.WriteLine("═══════════════════════════════════════════════════════════════════");
            context.Writer.WriteLine($"AGENT SESSION LOG");
            context.Writer.WriteLine("═══════════════════════════════════════════════════════════════════");
            context.Writer.WriteLine($"Session ID:        {sessionId}");
            context.Writer.WriteLine($"Command:           {command}");
            context.Writer.WriteLine($"Working Directory: {workingDirectory ?? "(default)"}");
            context.Writer.WriteLine($"Started:           {now:yyyy-MM-dd HH:mm:ss.fff}");
            context.Writer.WriteLine("═══════════════════════════════════════════════════════════════════");
            context.Writer.WriteLine();

            _sessions[sessionId] = context;
        }

        return Task.CompletedTask;
    }

    public Task LogOutputAsync(string sessionId, byte[] data)
    {
        if (_disposed || data.Length == 0) return Task.CompletedTask;

        var text = Encoding.UTF8.GetString(data);
        return LogOutputAsync(sessionId, text);
    }

    public Task LogOutputAsync(string sessionId, string text)
    {
        if (_disposed || string.IsNullOrEmpty(text)) return Task.CompletedTask;

        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var context) || context.Writer == null)
                return Task.CompletedTask;

            var timestamp = _clock.Now.ToString("HH:mm:ss.fff");

            // Write output with timestamp prefix for each line
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                // Don't add timestamp for empty lines in output (preserves formatting)
                if (string.IsNullOrEmpty(line))
                {
                    context.Writer.WriteLine();
                }
                else
                {
                    context.Writer.WriteLine($"[{timestamp}] [OUT] {line}");
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task LogInputAsync(string sessionId, byte[] data)
    {
        if (_disposed || data.Length == 0) return Task.CompletedTask;

        var text = Encoding.UTF8.GetString(data);
        return LogInputAsync(sessionId, text);
    }

    public Task LogInputAsync(string sessionId, string text)
    {
        if (_disposed || string.IsNullOrEmpty(text)) return Task.CompletedTask;

        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var context) || context.Writer == null)
                return Task.CompletedTask;

            var timestamp = _clock.Now.ToString("HH:mm:ss.fff");

            // Format input - show control characters in readable form
            var displayText = FormatInputForDisplay(text);

            context.Writer.WriteLine();
            context.Writer.WriteLine($"[{timestamp}] [INPUT] >>> {displayText}");
            context.Writer.WriteLine();
        }

        return Task.CompletedTask;
    }

    public Task EndSessionAsync(string sessionId, int? exitCode)
    {
        if (_disposed) return Task.CompletedTask;

        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var context))
                return Task.CompletedTask;

            if (context.Writer != null)
            {
                var now = _clock.Now;
                var duration = now - context.StartTime;

                context.Writer.WriteLine();
                context.Writer.WriteLine("═══════════════════════════════════════════════════════════════════");
                context.Writer.WriteLine($"SESSION ENDED");
                context.Writer.WriteLine("═══════════════════════════════════════════════════════════════════");
                context.Writer.WriteLine($"Ended:    {now:yyyy-MM-dd HH:mm:ss.fff}");
                context.Writer.WriteLine($"Duration: {duration:hh\\:mm\\:ss\\.fff}");
                context.Writer.WriteLine($"Exit Code: {exitCode?.ToString() ?? "(unknown)"}");
                context.Writer.WriteLine("═══════════════════════════════════════════════════════════════════");

                context.Writer.Dispose();
                context.Writer = null;
            }

            _sessions.Remove(sessionId);
        }

        return Task.CompletedTask;
    }

    public string? GetLogFilePath(string sessionId)
    {
        lock (_lock)
        {
            return _sessions.TryGetValue(sessionId, out var context) ? context.FilePath : null;
        }
    }

    private string BuildFilePath(string sessionId, DateTime timestamp)
    {
        // Structure: {base}/{yyyy-MM-dd}/{HH}/session-{sessionId}.log
        var datePart = timestamp.ToString("yyyy-MM-dd");
        var hourPart = timestamp.ToString("HH");
        var fileName = $"session-{sessionId}.log";

        return Path.Combine(_basePath, datePart, hourPart, fileName);
    }

    private static string FormatInputForDisplay(string text)
    {
        var sb = new StringBuilder();

        foreach (var c in text)
        {
            switch (c)
            {
                case '\r':
                    sb.Append("[CR]");
                    break;
                case '\n':
                    sb.Append("[LF]");
                    break;
                case '\t':
                    sb.Append("[TAB]");
                    break;
                case '\x03': // Ctrl+C
                    sb.Append("[Ctrl+C]");
                    break;
                case '\x04': // Ctrl+D
                    sb.Append("[Ctrl+D]");
                    break;
                case '\x1B': // Escape
                    sb.Append("[ESC]");
                    break;
                case '\x7F': // Backspace/Delete
                    sb.Append("[DEL]");
                    break;
                default:
                    if (c < 32)
                    {
                        sb.Append($"[^{(char)(c + 64)}]");
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }

        return sb.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        List<SessionLogContext> contexts;
        lock (_lock)
        {
            contexts = _sessions.Values.ToList();
            _sessions.Clear();
        }

        foreach (var context in contexts)
        {
            if (context.Writer != null)
            {
                await context.Writer.DisposeAsync();
            }
        }
    }

    private class SessionLogContext
    {
        public string SessionId { get; set; } = "";
        public DateTime StartTime { get; set; }
        public string FilePath { get; set; } = "";
        public StreamWriter? Writer { get; set; }
    }
}

/// <summary>
/// Null implementation of agent session logger for when logging is disabled.
/// All methods are no-ops.
/// </summary>
public class NullAgentSessionLogger : IAgentSessionLogger
{
    public Task StartSessionAsync(string sessionId, string command, string? workingDirectory) => Task.CompletedTask;
    public Task LogOutputAsync(string sessionId, byte[] data) => Task.CompletedTask;
    public Task LogOutputAsync(string sessionId, string text) => Task.CompletedTask;
    public Task LogInputAsync(string sessionId, byte[] data) => Task.CompletedTask;
    public Task LogInputAsync(string sessionId, string text) => Task.CompletedTask;
    public Task EndSessionAsync(string sessionId, int? exitCode) => Task.CompletedTask;
    public string? GetLogFilePath(string sessionId) => null;
}
