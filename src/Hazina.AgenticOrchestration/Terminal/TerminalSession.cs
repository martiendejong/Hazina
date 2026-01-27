using System.Buffers;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Hazina.AgenticOrchestration.Terminal;

/// <summary>
/// Manages a single terminal session with a spawned process.
/// Provides real-time bidirectional I/O with minimal latency.
///
/// Key design decisions (per expert panel):
/// - Uses raw BaseStream, NOT OutputDataReceived (which is line-buffered)
/// - Immediate byte forwarding for sub-50ms latency
/// - Preserves ANSI escape sequences for proper terminal rendering
///
/// LIMITATION: Uses pipe-based I/O redirection, NOT ConPTY.
/// This means interactive TUI applications (Claude CLI, vim, htop, etc.)
/// will not render their full interface because they detect stdout is not a TTY.
/// For full TUI support, implement ConPTY (Windows Pseudo Console).
/// See: https://devblogs.microsoft.com/commandline/windows-command-line-introducing-the-windows-pseudo-console-conpty/
/// </summary>
public class TerminalSession : ITerminalSession
{
    private readonly ILogger<TerminalSession>? _logger;
    private readonly TerminalSessionConfig _config;
    private readonly object _historyLock = new();
    private readonly MemoryStream _outputHistory = new();
    private readonly object _stateLock = new();
    private Process? _process;
    private StreamWriter? _stdin;
    private Stream? _stdout;
    private Stream? _stderr;
    private CancellationTokenSource? _cts;
    private Task? _outputTask;
    private Task? _stderrTask;
    private bool _disposed;
    private DateTime _lastOutputTime = DateTime.UtcNow;
    private bool _waitingForInput;
    private string _recentOutput = "";

    public string SessionId { get; }
    public DateTime StartedAt { get; private set; }
    public string Command => _config.Command;
    public bool IsRunning => _process?.HasExited == false;
    public int? ExitCode => _process?.HasExited == true ? _process.ExitCode : null;

    /// <summary>
    /// Whether the session appears to be waiting for user input
    /// </summary>
    public bool WaitingForInput
    {
        get
        {
            lock (_stateLock)
            {
                if (!IsRunning) return false;
                if (!_waitingForInput) return false;
                // Only show "Question" after 1 full second of no output
                var idleTime = DateTime.UtcNow - _lastOutputTime;
                return idleTime.TotalMilliseconds >= 1000;
            }
        }
    }

    public event Action<byte[]>? OnOutput;
    public event Action<int>? OnExit;

    /// <summary>
    /// Get all output history as bytes
    /// </summary>
    public byte[] GetOutputHistory()
    {
        lock (_historyLock)
        {
            return _outputHistory.ToArray();
        }
    }

    private static readonly string[] QuestionPatterns = new[]
    {
        "│ ●", "│ ○", "| ●", "| ○", "❯", ">",
        "(y/n)", "[Y/n]", "[y/N]", "(Y/n)", "? ",
        "Allow", "Deny", "press enter", "Press Enter",
        "continue?", "proceed?",
    };

    private void DetectQuestionInOutput(string text)
    {
        lock (_stateLock)
        {
            _recentOutput = (_recentOutput + text);
            if (_recentOutput.Length > 500)
                _recentOutput = _recentOutput.Substring(_recentOutput.Length - 500);
            _waitingForInput = QuestionPatterns.Any(p =>
                _recentOutput.Contains(p, StringComparison.OrdinalIgnoreCase));
            if (_waitingForInput)
                _logger?.LogDebug("Question pattern detected in session {SessionId}", SessionId);
        }
    }

    private void ResetWaitingState()
    {
        lock (_stateLock)
        {
            _waitingForInput = false;
            _recentOutput = "";
        }
    }

    public TerminalSession(string sessionId, TerminalSessionConfig config, ILogger<TerminalSession>? logger = null)
    {
        SessionId = sessionId;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Start the process and begin streaming output
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_process != null)
            throw new InvalidOperationException("Session already started");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var startInfo = new ProcessStartInfo
        {
            FileName = _config.Command,
            Arguments = string.Join(" ", _config.Arguments),
            WorkingDirectory = _config.WorkingDirectory ?? Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // Set terminal environment variables for proper ANSI support
        startInfo.Environment["TERM"] = "xterm-256color";
        startInfo.Environment["COLORTERM"] = "truecolor";
        startInfo.Environment["FORCE_COLOR"] = "3";
        startInfo.Environment["CLICOLOR_FORCE"] = "1";

        // Remove NO_COLOR if it exists (we want colors!)
        startInfo.Environment.Remove("NO_COLOR");

        // Set terminal size environment variables
        startInfo.Environment["COLUMNS"] = _config.Columns.ToString();
        startInfo.Environment["LINES"] = _config.Rows.ToString();

        // Add custom environment variables
        foreach (var (key, value) in _config.Environment)
        {
            startInfo.Environment[key] = value;
        }

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        _process.Exited += (s, e) =>
        {
            _logger?.LogInformation("Process exited with code {ExitCode} for session {SessionId}",
                _process.ExitCode, SessionId);
            OnExit?.Invoke(_process.ExitCode);
        };

        _logger?.LogInformation("Starting process: {Command} {Args} in {WorkingDir}",
            _config.Command, string.Join(" ", _config.Arguments), startInfo.WorkingDirectory);

        _process.Start();
        StartedAt = DateTime.UtcNow;

        // Get raw streams - critical for low latency!
        _stdin = _process.StandardInput;
        _stdin.AutoFlush = true; // Ensure immediate write

        _stdout = _process.StandardOutput.BaseStream;
        _stderr = _process.StandardError.BaseStream;

        // Start reading output asynchronously
        _outputTask = ReadOutputAsync(_stdout, "stdout", _cts.Token);

        if (!_config.MergeStderr)
        {
            _stderrTask = ReadOutputAsync(_stderr, "stderr", _cts.Token);
        }
        else
        {
            // For merged stderr, read it but forward to same output
            _stderrTask = ReadOutputAsync(_stderr, "stderr", _cts.Token);
        }

        _logger?.LogInformation("Session {SessionId} started successfully, PID: {Pid}",
            SessionId, _process.Id);
    }

    /// <summary>
    /// Read from a stream and fire output events.
    /// Uses small buffer and immediate forwarding for low latency.
    /// </summary>
    private async Task ReadOutputAsync(Stream stream, string streamName, CancellationToken ct)
    {
        // Use pooled buffer to reduce GC pressure
        var buffer = ArrayPool<byte>.Shared.Rent(4096);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                int bytesRead;
                try
                {
                    bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ex) when (ex.Message.Contains("pipe"))
                {
                    // Pipe closed - process ended
                    _logger?.LogDebug("Stream {StreamName} pipe closed for session {SessionId}",
                        streamName, SessionId);
                    break;
                }

                if (bytesRead == 0)
                {
                    // EOF - process ended
                    _logger?.LogDebug("Stream {StreamName} EOF for session {SessionId}",
                        streamName, SessionId);
                    break;
                }

                // Copy data for the event (buffer will be reused)
                var data = new byte[bytesRead];
                Array.Copy(buffer, data, bytesRead);

                // Store in history buffer
                lock (_historyLock)
                {
                    _outputHistory.Write(data, 0, data.Length);
                }

                // Update state for question detection
                lock (_stateLock)
                {
                    _lastOutputTime = DateTime.UtcNow;
                }

                // Try to detect if this output contains a question
                try
                {
                    var text = Encoding.UTF8.GetString(data);
                    DetectQuestionInOutput(text);
                }
                catch { /* Ignore encoding errors */ }

                // Fire event immediately - no batching!
                try
                {
                    OnOutput?.Invoke(data);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in OnOutput handler for session {SessionId}", SessionId);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Write input to the process stdin
    /// </summary>
    public async Task WriteInputAsync(byte[] data, CancellationToken ct = default)
    {
        if (_stdin == null || !IsRunning)
        {
            _logger?.LogWarning("Cannot write to session {SessionId}: process not running", SessionId);
            return;
        }

        // Reset waiting state since user is providing input
        ResetWaitingState();

        try
        {
            // Write raw bytes to stdin
            await _stdin.BaseStream.WriteAsync(data, ct);
            await _stdin.BaseStream.FlushAsync(ct);

            _logger?.LogTrace("Wrote {ByteCount} bytes to session {SessionId}", data.Length, SessionId);
        }
        catch (IOException ex)
        {
            _logger?.LogWarning(ex, "Failed to write to session {SessionId}", SessionId);
        }
    }

    /// <summary>
    /// Write string input (convenience method)
    /// </summary>
    public Task WriteInputAsync(string text, CancellationToken ct = default)
    {
        return WriteInputAsync(Encoding.UTF8.GetBytes(text), ct);
    }

    /// <summary>
    /// Send a signal to the process (e.g., Ctrl+C)
    /// </summary>
    public async Task SendSignalAsync(TerminalSignal signal)
    {
        if (_stdin == null || !IsRunning)
            return;

        switch (signal)
        {
            case TerminalSignal.Interrupt:
                // Send Ctrl+C (ASCII 0x03)
                await WriteInputAsync(new byte[] { 0x03 });
                _logger?.LogInformation("Sent SIGINT to session {SessionId}", SessionId);
                break;

            case TerminalSignal.Quit:
                // Send Ctrl+\ (ASCII 0x1C)
                await WriteInputAsync(new byte[] { 0x1C });
                _logger?.LogInformation("Sent SIGQUIT to session {SessionId}", SessionId);
                break;

            case TerminalSignal.Terminate:
                _process?.Kill(entireProcessTree: false);
                _logger?.LogInformation("Sent SIGTERM to session {SessionId}", SessionId);
                break;

            case TerminalSignal.Kill:
                _process?.Kill(entireProcessTree: true);
                _logger?.LogInformation("Sent SIGKILL to session {SessionId}", SessionId);
                break;
        }
    }

    /// <summary>
    /// Resize the terminal (updates environment, some processes respect this)
    /// </summary>
    public Task ResizeAsync(int columns, int rows)
    {
        // For basic process redirection, resize doesn't work directly.
        // This would require ConPTY on Windows for true resize support.
        // For now, log the resize request.
        _logger?.LogDebug("Resize request for session {SessionId}: {Cols}x{Rows}",
            SessionId, columns, rows);

        // Future: Implement ConPTY for true resize support
        return Task.CompletedTask;
    }

    /// <summary>
    /// Terminate the process
    /// </summary>
    public async Task TerminateAsync()
    {
        if (_process == null || _process.HasExited)
            return;

        _logger?.LogInformation("Terminating session {SessionId}", SessionId);

        try
        {
            // First try graceful termination
            await SendSignalAsync(TerminalSignal.Interrupt);

            // Wait briefly for graceful exit
            var exited = await WaitForExitAsync(TimeSpan.FromSeconds(2));

            if (!exited)
            {
                // Force kill
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error terminating session {SessionId}", SessionId);
        }
    }

    private async Task<bool> WaitForExitAsync(TimeSpan timeout)
    {
        if (_process == null)
            return true;

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await _process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        _logger?.LogDebug("Disposing session {SessionId}", SessionId);

        // Cancel output reading
        _cts?.Cancel();

        // Wait for output tasks to complete
        if (_outputTask != null)
        {
            try { await _outputTask; } catch { /* ignore */ }
        }
        if (_stderrTask != null)
        {
            try { await _stderrTask; } catch { /* ignore */ }
        }

        // Terminate process if still running
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch { /* ignore */ }
        }

        _stdin?.Dispose();
        _process?.Dispose();
        _cts?.Dispose();

        _logger?.LogInformation("Session {SessionId} disposed", SessionId);
    }
}
