using System.Buffers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace Hazina.AgenticOrchestration.Terminal.ConPty;

/// <summary>
/// Terminal session implementation using Windows ConPTY (Pseudo Console).
/// Provides full TTY emulation for interactive TUI applications like Claude CLI, vim, htop, etc.
///
/// Key features:
/// - Full ANSI escape sequence support
/// - Proper TTY detection by child processes
/// - Terminal resize support
/// - Low-latency bidirectional I/O
/// </summary>
public class ConPtyTerminalSession : ITerminalSession
{
    private readonly ILogger<ConPtyTerminalSession>? _logger;
    private readonly TerminalSessionConfig _config;
    private readonly object _historyLock = new();
    private readonly MemoryStream _outputHistory = new();
    private readonly object _stateLock = new();

    private PseudoConsolePipe? _inputPipe;
    private PseudoConsolePipe? _outputPipe;
    private PseudoConsole? _pseudoConsole;
    private ConPtyProcess? _process;
    private FileStream? _inputWriter;
    private FileStream? _outputReader;
    private CancellationTokenSource? _cts;
    private Task? _outputTask;
    private bool _disposed;
    private DateTime _lastOutputTime = DateTime.UtcNow;
    private bool _waitingForInput;
    private string _recentOutput = "";

    public string SessionId { get; }
    public DateTime StartedAt { get; private set; }
    public string Command => _config.Command;
    public bool IsRunning => _process?.HasExited == false;
    public int? ExitCode => _process?.ExitCode;

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

                // Only return true if we detected a question pattern
                // AND there's been no output for at least 1 second (to avoid flicker during streaming)
                if (!_waitingForInput) return false;

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

    // Patterns that indicate Claude is asking a question or waiting for input
    private static readonly string[] QuestionPatterns = new[]
    {
        "│ ●", // Claude's selection UI (selected option)
        "│ ○", // Claude's selection UI (unselected option)
        "| ●", // ASCII fallback for selection UI
        "| ○", // ASCII fallback for selection UI
        "❯", // Arrow prompt
        ">", // Simple prompt (at end of line)
        "(y/n)", // Yes/no prompt
        "[Y/n]", // Yes/no prompt
        "[y/N]", // Yes/no prompt
        "(Y/n)", // Yes/no prompt variant
        "? ", // Question with space
        "Allow", // Permission prompts often contain "Allow"
        "Deny", // Permission prompts
        "press enter", // Common prompt
        "Press Enter", // Common prompt
        "continue?", // Continuation prompt
        "proceed?", // Proceed prompt
    };

    private void DetectQuestionInOutput(string text)
    {
        lock (_stateLock)
        {
            // Keep last 500 chars of output for pattern matching
            _recentOutput = (_recentOutput + text);
            if (_recentOutput.Length > 500)
            {
                _recentOutput = _recentOutput.Substring(_recentOutput.Length - 500);
            }

            // Check for question patterns - case insensitive for text patterns
            _waitingForInput = QuestionPatterns.Any(pattern =>
                _recentOutput.Contains(pattern, StringComparison.OrdinalIgnoreCase));

            if (_waitingForInput)
            {
                _logger?.LogDebug("Question pattern detected in session {SessionId}", SessionId);
            }
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

    public ConPtyTerminalSession(string sessionId, TerminalSessionConfig config, ILogger<ConPtyTerminalSession>? logger = null)
    {
        SessionId = sessionId;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Start the ConPTY session and begin streaming output.
    /// </summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        if (_process != null)
            throw new InvalidOperationException("Session already started");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            // Create pipes for communication
            // Input: We write to inputPipe.WriteHandle, ConPTY reads from inputPipe.ReadHandle
            // Output: ConPTY writes to outputPipe.WriteHandle, we read from outputPipe.ReadHandle
            _inputPipe = new PseudoConsolePipe();
            _outputPipe = new PseudoConsolePipe();

            // Create the pseudo console
            _pseudoConsole = PseudoConsole.Create(
                _inputPipe.ReadHandle,
                _outputPipe.WriteHandle,
                (short)_config.Columns,
                (short)_config.Rows);

            _logger?.LogInformation(
                "Created ConPTY pseudo console {Cols}x{Rows} for session {SessionId}",
                _config.Columns, _config.Rows, SessionId);

            // Build the command line
            var commandLine = BuildCommandLine();

            // Build environment variables
            var environment = BuildEnvironment();

            // Start the process attached to the pseudo console
            _process = ConPtyProcess.Start(
                commandLine,
                _config.WorkingDirectory ?? Directory.GetCurrentDirectory(),
                _pseudoConsole,
                environment);

            StartedAt = DateTime.UtcNow;

            _logger?.LogInformation(
                "Started ConPTY process for session {SessionId}, PID: {Pid}, Command: {Command}",
                SessionId, _process.ProcessId, commandLine);

            // Create streams for reading/writing
            // Note: We use isAsync: false because CreatePipe doesn't create overlapped handles
            // We'll use Task.Run() to avoid blocking the main thread
            _inputWriter = new FileStream(
                new SafeFileHandle(_inputPipe.WriteHandle.DangerousGetHandle(), ownsHandle: false),
                FileAccess.Write,
                bufferSize: 256,
                isAsync: false);

            // We read from the output pipe's read handle
            _outputReader = new FileStream(
                new SafeFileHandle(_outputPipe.ReadHandle.DangerousGetHandle(), ownsHandle: false),
                FileAccess.Read,
                bufferSize: 4096,
                isAsync: false);

            // Start reading output
            _outputTask = ReadOutputAsync(_cts.Token);

            // Monitor for process exit
            _ = MonitorProcessExitAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start ConPTY session {SessionId}", SessionId);
            Cleanup();
            throw;
        }

        return Task.CompletedTask;
    }

    private string BuildCommandLine()
    {
        var sb = new StringBuilder();

        // If the command contains spaces, it should be quoted
        if (_config.Command.Contains(' '))
        {
            sb.Append('"').Append(_config.Command).Append('"');
        }
        else
        {
            sb.Append(_config.Command);
        }

        // Add arguments
        foreach (var arg in _config.Arguments)
        {
            sb.Append(' ');
            if (arg.Contains(' ') || arg.Contains('"'))
            {
                // Escape quotes and wrap in quotes
                sb.Append('"').Append(arg.Replace("\"", "\\\"")).Append('"');
            }
            else
            {
                sb.Append(arg);
            }
        }

        return sb.ToString();
    }

    private Dictionary<string, string> BuildEnvironment()
    {
        var env = new Dictionary<string, string>();

        // Copy current environment
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            env[entry.Key?.ToString() ?? ""] = entry.Value?.ToString() ?? "";
        }

        // Set terminal type for proper ANSI support
        env["TERM"] = "xterm-256color";
        env["COLORTERM"] = "truecolor";
        env["FORCE_COLOR"] = "3";
        env["CLICOLOR_FORCE"] = "1";

        // Remove NO_COLOR if present
        env.Remove("NO_COLOR");

        // Set terminal size
        env["COLUMNS"] = _config.Columns.ToString();
        env["LINES"] = _config.Rows.ToString();

        // Add custom environment variables
        foreach (var (key, value) in _config.Environment)
        {
            env[key] = value;
        }

        return env;
    }

    private async Task ReadOutputAsync(CancellationToken ct)
    {
        _logger?.LogInformation("Starting output read loop for ConPTY session {SessionId}", SessionId);

        var buffer = ArrayPool<byte>.Shared.Rent(4096);

        try
        {
            while (!ct.IsCancellationRequested && _outputReader != null)
            {
                int bytesRead;
                try
                {
                    // Use synchronous read on background thread since pipe handles don't support async
                    var reader = _outputReader;
                    _logger?.LogDebug("Waiting for output from ConPTY session {SessionId}...", SessionId);
                    bytesRead = await Task.Run(() => reader.Read(buffer, 0, buffer.Length), ct);
                    _logger?.LogDebug("Read {ByteCount} bytes from ConPTY session {SessionId}", bytesRead, SessionId);
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogDebug("Output read cancelled for session {SessionId}", SessionId);
                    break;
                }
                catch (IOException ex)
                {
                    _logger?.LogInformation(ex, "Output stream closed for session {SessionId}", SessionId);
                    break;
                }
                catch (ObjectDisposedException)
                {
                    _logger?.LogDebug("Output stream disposed for session {SessionId}", SessionId);
                    break;
                }

                if (bytesRead == 0)
                {
                    _logger?.LogInformation("Output EOF for session {SessionId}", SessionId);
                    break;
                }

                // Copy data for the event
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

                _logger?.LogDebug("ConPTY output: {ByteCount} bytes for session {SessionId}, total history: {HistorySize} bytes",
                    bytesRead, SessionId, _outputHistory.Length);

                // Fire event immediately
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
            _logger?.LogInformation("Output read loop ended for ConPTY session {SessionId}", SessionId);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task MonitorProcessExitAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _process != null && !_process.HasExited)
            {
                await Task.Delay(100, ct);
            }

            if (_process != null)
            {
                var exitCode = _process.ExitCode ?? -1;
                _logger?.LogInformation(
                    "ConPTY process exited with code {ExitCode} for session {SessionId}",
                    exitCode, SessionId);

                OnExit?.Invoke(exitCode);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error monitoring process exit for session {SessionId}", SessionId);
        }
    }

    /// <summary>
    /// Write input to the terminal.
    /// </summary>
    public async Task WriteInputAsync(byte[] data, CancellationToken ct = default)
    {
        if (_inputWriter == null || !IsRunning)
        {
            _logger?.LogWarning("Cannot write to session {SessionId}: not running", SessionId);
            return;
        }

        // Reset waiting state since user is providing input
        ResetWaitingState();

        try
        {
            // Use synchronous write on background thread since pipe handles don't support async
            var writer = _inputWriter;
            await Task.Run(() =>
            {
                writer.Write(data, 0, data.Length);
                writer.Flush();
            }, ct);

            _logger?.LogTrace("Wrote {ByteCount} bytes to ConPTY session {SessionId}", data.Length, SessionId);
        }
        catch (IOException ex)
        {
            _logger?.LogWarning(ex, "Failed to write to session {SessionId}", SessionId);
        }
    }

    /// <summary>
    /// Write string input (convenience method).
    /// </summary>
    public Task WriteInputAsync(string text, CancellationToken ct = default)
    {
        return WriteInputAsync(Encoding.UTF8.GetBytes(text), ct);
    }

    /// <summary>
    /// Send a signal to the process.
    /// </summary>
    public async Task SendSignalAsync(TerminalSignal signal)
    {
        if (!IsRunning)
            return;

        switch (signal)
        {
            case TerminalSignal.Interrupt:
                // Send Ctrl+C (ASCII 0x03)
                await WriteInputAsync(new byte[] { 0x03 });
                _logger?.LogInformation("Sent SIGINT (Ctrl+C) to ConPTY session {SessionId}", SessionId);
                break;

            case TerminalSignal.Quit:
                // Send Ctrl+\ (ASCII 0x1C)
                await WriteInputAsync(new byte[] { 0x1C });
                _logger?.LogInformation("Sent SIGQUIT to ConPTY session {SessionId}", SessionId);
                break;

            case TerminalSignal.Terminate:
            case TerminalSignal.Kill:
                _process?.Kill();
                _logger?.LogInformation("Killed ConPTY process for session {SessionId}", SessionId);
                break;
        }
    }

    /// <summary>
    /// Resize the terminal.
    /// </summary>
    public Task ResizeAsync(int columns, int rows)
    {
        if (_pseudoConsole == null || _disposed)
            return Task.CompletedTask;

        try
        {
            _pseudoConsole.Resize((short)columns, (short)rows);
            _logger?.LogInformation(
                "Resized ConPTY to {Cols}x{Rows} for session {SessionId}",
                columns, rows, SessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to resize ConPTY for session {SessionId}", SessionId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Terminate the process.
    /// </summary>
    public async Task TerminateAsync()
    {
        if (_process == null || _process.HasExited)
            return;

        _logger?.LogInformation("Terminating ConPTY session {SessionId}", SessionId);

        try
        {
            // Try graceful termination first
            await SendSignalAsync(TerminalSignal.Interrupt);

            // Wait briefly for graceful exit
            if (!_process.WaitForExit(2000))
            {
                // Force kill
                _process.Kill();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error terminating ConPTY session {SessionId}", SessionId);
        }
    }

    private void Cleanup()
    {
        _inputWriter?.Dispose();
        _inputWriter = null;

        _outputReader?.Dispose();
        _outputReader = null;

        _process?.Dispose();
        _process = null;

        _pseudoConsole?.Dispose();
        _pseudoConsole = null;

        _inputPipe?.Dispose();
        _inputPipe = null;

        _outputPipe?.Dispose();
        _outputPipe = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        _logger?.LogDebug("Disposing ConPTY session {SessionId}", SessionId);

        // Cancel output reading
        _cts?.Cancel();

        // Wait for output task
        if (_outputTask != null)
        {
            try { await _outputTask; } catch { /* ignore */ }
        }

        // Terminate process if still running
        if (_process != null && !_process.HasExited)
        {
            try { _process.Kill(); } catch { /* ignore */ }
        }

        Cleanup();
        _cts?.Dispose();

        _logger?.LogInformation("ConPTY session {SessionId} disposed", SessionId);
    }
}
