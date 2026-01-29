using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hazina.App.HazinaCoder.Core.Identity;

namespace Hazina.App.HazinaCoder.Core.Memory;

/// <summary>
/// Reflection log - episodic memory storage
/// Documents sessions, learnings, patterns for continuous improvement
/// </summary>
public class ReflectionLog
{
    private readonly string _logPath;

    public ReflectionLog(string logPath)
    {
        _logPath = logPath;
    }

    public List<ReflectionEntry> RecentEntries { get; private set; } = new();

    /// <summary>
    /// Load recent entries from reflection log
    /// </summary>
    public async Task LoadRecentAsync(int count = 50)
    {
        if (!File.Exists(_logPath))
        {
            // Create initial reflection log
            await InitializeLogAsync();
            return;
        }

        var lines = await File.ReadAllLinesAsync(_logPath);
        RecentEntries = ParseEntries(lines).TakeLast(count).ToList();
    }

    /// <summary>
    /// Add new reflection entry
    /// </summary>
    public async Task AddEntryAsync(ReflectionEntry entry)
    {
        var markdown = FormatEntry(entry);

        await File.AppendAllTextAsync(_logPath, markdown);
        RecentEntries.Add(entry);

        // Keep only recent entries in memory
        if (RecentEntries.Count > 100)
            RecentEntries = RecentEntries.TakeLast(100).ToList();
    }

    /// <summary>
    /// Search for similar past experiences
    /// </summary>
    public List<ReflectionEntry> FindSimilar(string query)
    {
        return RecentEntries
            .Where(e => e.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                       e.Patterns.Any(p => p.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Extract patterns from recent entries
    /// </summary>
    public List<string> ExtractPatterns(int lookbackCount = 20)
    {
        var recentPatterns = RecentEntries
            .TakeLast(lookbackCount)
            .SelectMany(e => e.Patterns)
            .GroupBy(p => p)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key} (seen {g.Count()}x)")
            .ToList();

        return recentPatterns;
    }

    private async Task InitializeLogAsync()
    {
        var initialContent = @"# HazinaCoder Reflection Log

**Purpose:** Episodic memory - document sessions, learnings, patterns
**Created:** " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + @"

---

## Session Format

Each session entry includes:
- **Timestamp:** When the session occurred
- **Duration:** How long the session lasted
- **Summary:** What was accomplished
- **Goals:** What goals were pursued
- **Patterns:** What patterns emerged
- **Learnings:** Key insights and lessons

---

";
        await File.WriteAllTextAsync(_logPath, initialContent);
    }

    private List<ReflectionEntry> ParseEntries(string[] lines)
    {
        // TODO: Parse markdown format reflection log
        // For now, return empty list
        return new List<ReflectionEntry>();
    }

    private string FormatEntry(ReflectionEntry entry)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\n## {entry.Timestamp:yyyy-MM-dd HH:mm} - Session ({entry.SessionDuration:hh\\:mm})\n");
        sb.AppendLine($"**Summary:** {entry.Summary}\n");

        if (entry.Goals.Any())
        {
            sb.AppendLine("**Goals:**");
            foreach (var goal in entry.Goals)
            {
                sb.AppendLine($"- [{goal.Status}] {goal.Description}");
            }
            sb.AppendLine();
        }

        if (entry.Patterns.Any())
        {
            sb.AppendLine("**Patterns:**");
            foreach (var pattern in entry.Patterns)
            {
                sb.AppendLine($"- {pattern}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---\n");
        return sb.ToString();
    }
}

/// <summary>
/// Single reflection entry - one session episode
/// </summary>
public class ReflectionEntry
{
    public DateTime Timestamp { get; set; }
    public TimeSpan SessionDuration { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<Goal> Goals { get; set; } = new();
    public List<string> Patterns { get; set; } = new();
    public List<string> Learnings { get; set; } = new();
}
