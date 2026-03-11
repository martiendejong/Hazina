using Hazina.AI.OpenCode.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Hazina.AI.OpenCode;

/// <summary>
/// Tracks OpenCode service usage with persistent JSONL storage
/// </summary>
public class UsageTracker
{
    private readonly ILogger<UsageTracker> _logger;
    private readonly string _logFilePath;
    private readonly ConcurrentBag<UsageRecord> _inMemoryCache;
    private readonly object _fileLock = new();

    public UsageTracker(ILogger<UsageTracker> logger)
    {
        _logger = logger;

        // Store usage logs in E:\jengo\documents\temp\ (working files location)
        var logDirectory = Path.Combine("E:", "jengo", "documents", "temp");
        Directory.CreateDirectory(logDirectory);
        _logFilePath = Path.Combine(logDirectory, "opencode-usage.jsonl");

        _inMemoryCache = new ConcurrentBag<UsageRecord>();

        _logger.LogInformation("UsageTracker initialized. Log file: {LogFile}", _logFilePath);
    }

    /// <summary>
    /// Log a usage record
    /// </summary>
    public void LogUsage(UsageRecord record)
    {
        try
        {
            // Add to in-memory cache
            _inMemoryCache.Add(record);

            // Append to JSONL file (one JSON object per line)
            var json = JsonSerializer.Serialize(record);

            lock (_fileLock)
            {
                File.AppendAllText(_logFilePath, json + Environment.NewLine);
            }

            _logger.LogDebug("Usage logged: Agent={Agent}, Operation={Operation}, Success={Success}, Duration={Duration}ms",
                record.AgentName, record.OperationType, record.Success, record.DurationMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log usage record");
        }
    }

    /// <summary>
    /// Get aggregated usage statistics
    /// </summary>
    public UsageStatistics GetStatistics()
    {
        try
        {
            // Load all records from file
            var allRecords = LoadAllRecords();

            if (allRecords.Count == 0)
            {
                return new UsageStatistics
                {
                    CallsByAgent = new Dictionary<string, int>(),
                    CallsByOperation = new Dictionary<string, int>(),
                    RecentErrors = new List<UsageRecord>()
                };
            }

            var successful = allRecords.Count(r => r.Success);
            var failed = allRecords.Count(r => !r.Success);

            return new UsageStatistics
            {
                TotalCalls = allRecords.Count,
                SuccessfulCalls = successful,
                FailedCalls = failed,
                CallsByAgent = allRecords.GroupBy(r => r.AgentName)
                    .ToDictionary(g => g.Key, g => g.Count()),
                CallsByOperation = allRecords.GroupBy(r => r.OperationType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                AverageDurationMs = allRecords.Any() ? (long)allRecords.Average(r => r.DurationMs) : 0,
                FirstCall = allRecords.Min(r => r.Timestamp),
                LastCall = allRecords.Max(r => r.Timestamp),
                RecentErrors = allRecords
                    .Where(r => !r.Success)
                    .OrderByDescending(r => r.Timestamp)
                    .Take(10)
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate usage statistics");
            return new UsageStatistics
            {
                CallsByAgent = new Dictionary<string, int>(),
                CallsByOperation = new Dictionary<string, int>(),
                RecentErrors = new List<UsageRecord>()
            };
        }
    }

    /// <summary>
    /// Load all usage records from JSONL file
    /// </summary>
    private List<UsageRecord> LoadAllRecords()
    {
        var records = new List<UsageRecord>();

        if (!File.Exists(_logFilePath))
        {
            return records;
        }

        lock (_fileLock)
        {
            foreach (var line in File.ReadLines(_logFilePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var record = JsonSerializer.Deserialize<UsageRecord>(line);
                    if (record != null)
                    {
                        records.Add(record);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize usage record: {Line}", line);
                }
            }
        }

        return records;
    }

    /// <summary>
    /// Get usage records for a specific time range
    /// </summary>
    public List<UsageRecord> GetRecordsSince(DateTime since)
    {
        return LoadAllRecords()
            .Where(r => r.Timestamp >= since)
            .OrderByDescending(r => r.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Clear old records (keep last N days)
    /// </summary>
    public void CleanupOldRecords(int keepDays = 30)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-keepDays);
            var records = LoadAllRecords();
            var recentRecords = records.Where(r => r.Timestamp >= cutoffDate).ToList();

            lock (_fileLock)
            {
                // Rewrite file with only recent records
                File.WriteAllLines(_logFilePath,
                    recentRecords.Select(r => JsonSerializer.Serialize(r)));
            }

            var removed = records.Count - recentRecords.Count;
            _logger.LogInformation("Cleaned up {Count} old usage records (older than {Days} days)",
                removed, keepDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old records");
        }
    }
}
