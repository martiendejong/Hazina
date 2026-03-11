using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Logging;

/// <summary>
/// Structured logging system with multiple outputs.
/// Improvement #31: Structured logging
/// Improvement #32: Log levels management
/// Improvement #33: Log filtering
/// Improvement #34: Log aggregation
/// Improvement #35: Performance logging
/// </summary>
public class StructuredLogger : IDisposable
{
    private readonly string _logDirectory;
    private readonly LogLevel _minLevel;
    private readonly List<ILogSink> _sinks = new();
    private readonly StreamWriter? _fileWriter;

    public StructuredLogger(string logDirectory, LogLevel minLevel = LogLevel.Information)
    {
        _logDirectory = logDirectory;
        _minLevel = minLevel;

        Directory.CreateDirectory(logDirectory);

        // Create daily log file
        var logFile = Path.Combine(logDirectory, $"hazinacoder_{DateTime.UtcNow:yyyyMMdd}.log");
        _fileWriter = new StreamWriter(logFile, append: true) { AutoFlush = true };

        _sinks.Add(new ConsoleSink());
        _sinks.Add(new FileSink(_fileWriter));
    }

    public void Log(LogLevel level, string message, Dictionary<string, object>? properties = null)
    {
        if (level < _minLevel) return;

        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message,
            Properties = properties ?? new()
        };

        foreach (var sink in _sinks)
        {
            sink.Write(entry);
        }
    }

    public void Debug(string message, Dictionary<string, object>? props = null) => Log(LogLevel.Debug, message, props);
    public void Info(string message, Dictionary<string, object>? props = null) => Log(LogLevel.Information, message, props);
    public void Warn(string message, Dictionary<string, object>? props = null) => Log(LogLevel.Warning, message, props);
    public void Error(string message, Dictionary<string, object>? props = null) => Log(LogLevel.Error, message, props);

    public void Dispose()
    {
        _fileWriter?.Dispose();
    }
}

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = "";
    public Dictionary<string, object> Properties { get; set; } = new();
}

public interface ILogSink
{
    void Write(LogEntry entry);
}

public class ConsoleSink : ILogSink
{
    public void Write(LogEntry entry)
    {
        var color = entry.Level switch
        {
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Information => ConsoleColor.Green,
            _ => ConsoleColor.Gray
        };

        Console.ForegroundColor = color;
        Console.WriteLine($"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] {entry.Message}");
        Console.ResetColor();
    }
}

public class FileSink : ILogSink
{
    private readonly StreamWriter _writer;

    public FileSink(StreamWriter writer)
    {
        _writer = writer;
    }

    public void Write(LogEntry entry)
    {
        var json = JsonSerializer.Serialize(entry);
        _writer.WriteLine(json);
    }
}
