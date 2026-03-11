namespace Hazina.AgenticOrchestration.Terminal;

/// <summary>
/// Represents a terminal session connected to a process.
/// Provides bidirectional I/O for true terminal emulation.
/// </summary>
public interface ITerminalSession : IAsyncDisposable
{
    /// <summary>
    /// Unique session identifier
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Whether the process is still running
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Process exit code (null if still running)
    /// </summary>
    int? ExitCode { get; }

    /// <summary>
    /// When the session was created
    /// </summary>
    DateTime StartedAt { get; }

    /// <summary>
    /// The command that was executed
    /// </summary>
    string Command { get; }

    /// <summary>
    /// Whether the session is waiting for user input (e.g., showing a question)
    /// </summary>
    bool WaitingForInput { get; }

    /// <summary>
    /// Dynamic title extracted from terminal output (e.g., from "STATUS: Title" pattern).
    /// Returns null if no title has been detected, in which case Command should be used.
    /// </summary>
    string? Title { get; }

    /// <summary>
    /// Get all output history as bytes
    /// </summary>
    byte[] GetOutputHistory();

    /// <summary>
    /// Start the terminal session and begin streaming output
    /// </summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Write input bytes to the process stdin
    /// </summary>
    Task WriteInputAsync(byte[] data, CancellationToken ct = default);

    /// <summary>
    /// Write input string to the process stdin (convenience method)
    /// </summary>
    Task WriteInputAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Signal the process (e.g., SIGINT for Ctrl+C)
    /// </summary>
    Task SendSignalAsync(TerminalSignal signal);

    /// <summary>
    /// Resize the terminal (if supported)
    /// </summary>
    Task ResizeAsync(int columns, int rows);

    /// <summary>
    /// Terminate the process
    /// </summary>
    Task TerminateAsync();

    /// <summary>
    /// Event fired when output is received from stdout/stderr
    /// </summary>
    event Action<byte[]>? OnOutput;

    /// <summary>
    /// Event fired when the process exits
    /// </summary>
    event Action<int>? OnExit;

    /// <summary>
    /// Event fired when the session title changes (detected from "STATUS: Title" in output)
    /// </summary>
    event Action<string>? OnTitleChanged;
}

/// <summary>
/// Terminal signals that can be sent to a process
/// </summary>
public enum TerminalSignal
{
    /// <summary>Interrupt (Ctrl+C)</summary>
    Interrupt,

    /// <summary>Quit (Ctrl+\)</summary>
    Quit,

    /// <summary>Terminate</summary>
    Terminate,

    /// <summary>Kill (force)</summary>
    Kill
}

/// <summary>
/// Configuration for starting a terminal session
/// </summary>
public class TerminalSessionConfig
{
    /// <summary>
    /// The command/executable to run
    /// </summary>
    public string Command { get; set; } = "claude";

    /// <summary>
    /// Command arguments
    /// </summary>
    public string[] Arguments { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Working directory for the process
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Additional environment variables
    /// </summary>
    public Dictionary<string, string> Environment { get; set; } = new();

    /// <summary>
    /// Number of terminal columns (default: 120)
    /// </summary>
    public int Columns { get; set; } = 120;

    /// <summary>
    /// Number of terminal rows (default: 30)
    /// </summary>
    public int Rows { get; set; } = 30;

    /// <summary>
    /// Whether to merge stderr into stdout (default: true for terminal-like behavior)
    /// </summary>
    public bool MergeStderr { get; set; } = true;

    /// <summary>
    /// Session timeout (default: 1 hour)
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromHours(1);
}
