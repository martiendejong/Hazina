using System;
using System.IO;
using System.Text;

namespace Hazina.App.HazinaCoder.Core.Learning;

/// <summary>
/// Enables agent to automatically update its own documentation.
/// Learns from mistakes, captures new patterns, updates instructions.
/// </summary>
/// <remarks>
/// Improvement #28: Self-Documentation Updates
///
/// The agent can:
/// - Update CLAUDE.md with new learnings
/// - Add entries to reflection logs
/// - Create new best practice documents
/// - Update configuration files
/// </remarks>
public class SelfDocumentation
{
    private readonly string _docsPath;
    private readonly StringBuilder _pendingUpdates = new();

    public SelfDocumentation(string docsPath)
    {
        _docsPath = docsPath;
    }

    /// <summary>
    /// Queue a documentation update
    /// </summary>
    public void QueueUpdate(string section, string content, string reason)
    {
        var update = $"""
            [Update: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]
            Section: {section}
            Reason: {reason}

            {content}

            ────────────────────────────────────────

            """;

        _pendingUpdates.AppendLine(update);
        Console.WriteLine($"[SelfDoc] Queued update for section: {section}");
    }

    /// <summary>
    /// Queue a reflection log entry
    /// </summary>
    public void AddReflection(string category, string lesson, string context)
    {
        var reflection = $"""
            ## {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - {category}

            **What happened:** {context}

            **Lesson learned:** {lesson}

            **Action:** Update relevant documentation

            """;

        _pendingUpdates.AppendLine(reflection);
        Console.WriteLine($"[SelfDoc] Added reflection: {category}");
    }

    /// <summary>
    /// Queue a best practice pattern
    /// </summary>
    public void AddBestPractice(string pattern, string description, string example)
    {
        var practice = $"""
            ### {pattern}

            **Description:** {description}

            **Example:**
            ```
            {example}
            ```

            **Added:** {DateTime.UtcNow:yyyy-MM-dd}

            """;

        _pendingUpdates.AppendLine(practice);
        Console.WriteLine($"[SelfDoc] Added best practice: {pattern}");
    }

    /// <summary>
    /// Commit all pending updates to disk
    /// </summary>
    public bool CommitUpdates()
    {
        if (_pendingUpdates.Length == 0)
        {
            Console.WriteLine("[SelfDoc] No pending updates to commit");
            return false;
        }

        try
        {
            var updateFile = Path.Combine(_docsPath, $"updates_{DateTime.UtcNow:yyyyMMdd_HHmmss}.md");
            File.WriteAllText(updateFile, _pendingUpdates.ToString());

            Console.WriteLine($"[SelfDoc] Committed updates to: {updateFile}");
            Console.WriteLine($"[SelfDoc] Total updates: {_pendingUpdates.Length} characters");

            _pendingUpdates.Clear();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SelfDoc] ERROR: Failed to commit updates: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get pending updates (for review before commit)
    /// </summary>
    public string GetPendingUpdates() => _pendingUpdates.ToString();

    /// <summary>
    /// Clear pending updates without committing
    /// </summary>
    public void ClearPending()
    {
        _pendingUpdates.Clear();
        Console.WriteLine("[SelfDoc] Cleared pending updates");
    }
}
