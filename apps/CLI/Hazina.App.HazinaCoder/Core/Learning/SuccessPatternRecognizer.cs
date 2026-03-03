using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Learning;

/// <summary>
/// Recognizes and logs successful patterns.
/// Tracks what works well and why, for future reference.
/// </summary>
/// <remarks>
/// Improvement #29: Success Pattern Recognition
///
/// Captures:
/// - What approach was used
/// - Why it succeeded
/// - Context in which it worked
/// - Preconditions required
/// - Outcomes achieved
/// </remarks>
public class SuccessPatternRecognizer
{
    private readonly List<SuccessPattern> _patterns = new();
    private readonly string _storageFile;

    public SuccessPatternRecognizer(string storageFile)
    {
        _storageFile = storageFile;
        LoadPatterns();
    }

    /// <summary>
    /// Record a successful outcome
    /// </summary>
    public void RecordSuccess(
        string task,
        string approach,
        string context,
        string outcome,
        Dictionary<string, string>? metadata = null)
    {
        var pattern = new SuccessPattern
        {
            Id = Guid.NewGuid(),
            Task = task,
            Approach = approach,
            Context = context,
            Outcome = outcome,
            Timestamp = DateTime.UtcNow,
            Metadata = metadata ?? new(),
            UseCount = 1
        };

        _patterns.Add(pattern);
        Console.WriteLine($"[Success] Recorded pattern: {task}");

        // Save immediately
        SavePatterns();
    }

    /// <summary>
    /// Find similar successful patterns for a given task
    /// </summary>
    public IEnumerable<SuccessPattern> FindSimilar(string task)
    {
        // TODO: Use embeddings for semantic similarity
        // For now, simple keyword matching
        var keywords = task.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return _patterns
            .Where(p => keywords.Any(k => p.Task.ToLower().Contains(k)))
            .OrderByDescending(p => p.UseCount)
            .ThenByDescending(p => p.Timestamp);
    }

    /// <summary>
    /// Mark a pattern as reused
    /// </summary>
    public void MarkReused(Guid patternId)
    {
        var pattern = _patterns.FirstOrDefault(p => p.Id == patternId);
        if (pattern != null)
        {
            pattern.UseCount++;
            pattern.LastUsed = DateTime.UtcNow;
            Console.WriteLine($"[Success] Pattern reused: {pattern.Task} (count: {pattern.UseCount})");
            SavePatterns();
        }
    }

    /// <summary>
    /// Get most successful patterns (by reuse count)
    /// </summary>
    public IEnumerable<SuccessPattern> GetTopPatterns(int count = 10)
    {
        return _patterns
            .OrderByDescending(p => p.UseCount)
            .ThenByDescending(p => p.Timestamp)
            .Take(count);
    }

    /// <summary>
    /// Save patterns to disk
    /// </summary>
    private void SavePatterns()
    {
        try
        {
            var json = JsonSerializer.Serialize(_patterns, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            System.IO.File.WriteAllText(_storageFile, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Success] ERROR: Failed to save patterns: {ex.Message}");
        }
    }

    /// <summary>
    /// Load patterns from disk
    /// </summary>
    private void LoadPatterns()
    {
        try
        {
            if (System.IO.File.Exists(_storageFile))
            {
                var json = System.IO.File.ReadAllText(_storageFile);
                var patterns = JsonSerializer.Deserialize<List<SuccessPattern>>(json);
                if (patterns != null)
                {
                    _patterns.AddRange(patterns);
                    Console.WriteLine($"[Success] Loaded {patterns.Count} patterns from disk");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Success] ERROR: Failed to load patterns: {ex.Message}");
        }
    }
}

/// <summary>
/// A recorded success pattern
/// </summary>
public class SuccessPattern
{
    public Guid Id { get; set; }
    public string Task { get; set; } = string.Empty;
    public string Approach { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public DateTime? LastUsed { get; set; }
    public int UseCount { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
