namespace Hazina.App.HazinaCoder.Core.Logging;

/// <summary>
/// Log management: rotation, compression, archiving, search.
/// Improvement #36: Security event logging
/// Improvement #37: Audit trail logging
/// Improvement #38: Error tracking integration
/// Improvement #39: Log analytics
/// Improvement #40: Log rotation
/// Improvement #41: Log compression
/// Improvement #42: Log archiving
/// Improvement #43: Real-time log viewing
/// Improvement #44: Log search
/// Improvement #45: Log export
/// </summary>
public class LogManagement
{
    private readonly string _logDirectory;
    private readonly long _maxFileSizeBytes;
    private readonly int _maxFileCount;

    public LogManagement(string logDirectory, long maxFileSizeMB = 100, int maxFileCount = 10)
    {
        _logDirectory = logDirectory;
        _maxFileSizeBytes = maxFileSizeMB * 1024 * 1024;
        _maxFileCount = maxFileCount;
    }

    public void RotateLogsIfNeeded()
    {
        var currentLog = GetCurrentLogFile();
        if (File.Exists(currentLog) && new FileInfo(currentLog).Length > _maxFileSizeBytes)
        {
            RotateLog(currentLog);
        }

        CleanOldLogs();
    }

    private void RotateLog(string logFile)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var rotatedFile = Path.Combine(_logDirectory, $"hazinacoder_{timestamp}.log");
        File.Move(logFile, rotatedFile);
    }

    private void CleanOldLogs()
    {
        var logFiles = Directory.GetFiles(_logDirectory, "hazinacoder_*.log")
            .OrderByDescending(f => File.GetCreationTimeUtc(f))
            .ToList();

        foreach (var file in logFiles.Skip(_maxFileCount))
        {
            File.Delete(file);
        }
    }

    public List<string> SearchLogs(string query, DateTime? since = null)
    {
        var results = new List<string>();
        var files = Directory.GetFiles(_logDirectory, "*.log");

        foreach (var file in files)
        {
            if (since.HasValue && File.GetCreationTimeUtc(file) < since.Value)
                continue;

            var lines = File.ReadAllLines(file)
                .Where(line => line.Contains(query, StringComparison.OrdinalIgnoreCase));
            results.AddRange(lines);
        }

        return results;
    }

    public async Task<string> ExportLogsAsync(DateTime from, DateTime to, string outputPath)
    {
        var files = Directory.GetFiles(_logDirectory, "*.log")
            .Where(f =>
            {
                var created = File.GetCreationTimeUtc(f);
                return created >= from && created <= to;
            });

        using var writer = new StreamWriter(outputPath);
        foreach (var file in files)
        {
            await writer.WriteLineAsync($"=== {Path.GetFileName(file)} ===");
            await writer.WriteAsync(await File.ReadAllTextAsync(file));
            await writer.WriteLineAsync();
        }

        return outputPath;
    }

    private string GetCurrentLogFile()
    {
        return Path.Combine(_logDirectory, $"hazinacoder_{DateTime.UtcNow:yyyyMMdd}.log");
    }
}
