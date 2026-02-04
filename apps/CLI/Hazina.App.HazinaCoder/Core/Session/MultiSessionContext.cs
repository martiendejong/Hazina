using System;
using System.Collections.Generic;
using System.Linq;

namespace Hazina.App.HazinaCoder.Core.Session;

/// <summary>
/// Maintains context across multiple sessions.
/// Learns from past sessions to provide better assistance over time.
/// </summary>
/// <remarks>
/// Improvement #36: Multi-Session Context
///
/// Tracks:
/// - Recent sessions and their outcomes
/// - Ongoing projects and their state
/// - Cross-session learnings
/// - User's long-term goals
/// </remarks>
public class MultiSessionContext
{
    private readonly List<SessionRecord> _sessions = new();
    private readonly Dictionary<string, ProjectContext> _projects = new();

    /// <summary>
    /// Start a new session
    /// </summary>
    public SessionRecord StartSession(string? projectName = null, string? template = null)
    {
        var session = new SessionRecord
        {
            Id = Guid.NewGuid(),
            StartTime = DateTime.UtcNow,
            ProjectName = projectName,
            TemplateName = template
        };

        _sessions.Add(session);

        Console.WriteLine($"[Context] Started session {session.Id:N}");
        if (projectName != null)
        {
            Console.WriteLine($"  Project: {projectName}");
            LoadProjectContext(projectName);
        }

        return session;
    }

    /// <summary>
    /// End current session
    /// </summary>
    public void EndSession(Guid sessionId, string summary = "")
    {
        var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session != null)
        {
            session.EndTime = DateTime.UtcNow;
            session.Summary = summary;
            session.Duration = session.EndTime.Value - session.StartTime;

            Console.WriteLine($"[Context] Ended session {sessionId:N}");
            Console.WriteLine($"  Duration: {session.Duration.TotalMinutes:F1} minutes");

            // Update project context if applicable
            if (!string.IsNullOrEmpty(session.ProjectName))
            {
                UpdateProjectContext(session.ProjectName, session);
            }
        }
    }

    /// <summary>
    /// Get recent sessions
    /// </summary>
    public IEnumerable<SessionRecord> GetRecentSessions(int count = 10)
    {
        return _sessions
            .OrderByDescending(s => s.StartTime)
            .Take(count);
    }

    /// <summary>
    /// Get sessions for a specific project
    /// </summary>
    public IEnumerable<SessionRecord> GetProjectSessions(string projectName)
    {
        return _sessions
            .Where(s => s.ProjectName == projectName)
            .OrderByDescending(s => s.StartTime);
    }

    /// <summary>
    /// Load project context
    /// </summary>
    private void LoadProjectContext(string projectName)
    {
        if (_projects.TryGetValue(projectName, out var context))
        {
            Console.WriteLine($"[Context] Loaded project context: {projectName}");
            Console.WriteLine($"  Last worked: {context.LastWorked:yyyy-MM-dd}");
            Console.WriteLine($"  Total sessions: {context.TotalSessions}");
            Console.WriteLine($"  Current phase: {context.CurrentPhase}");
        }
        else
        {
            // Create new project context
            _projects[projectName] = new ProjectContext
            {
                ProjectName = projectName,
                FirstSeen = DateTime.UtcNow
            };
            Console.WriteLine($"[Context] Created new project context: {projectName}");
        }
    }

    /// <summary>
    /// Update project context after session
    /// </summary>
    private void UpdateProjectContext(string projectName, SessionRecord session)
    {
        if (!_projects.TryGetValue(projectName, out var context))
        {
            context = new ProjectContext { ProjectName = projectName };
            _projects[projectName] = context;
        }

        context.LastWorked = DateTime.UtcNow;
        context.TotalSessions++;
        context.TotalDuration += session.Duration;

        // TODO: Use LLM to summarize progress and update phase
    }

    /// <summary>
    /// Get project context
    /// </summary>
    public ProjectContext? GetProjectContext(string projectName)
    {
        return _projects.TryGetValue(projectName, out var context) ? context : null;
    }
}

/// <summary>
/// Record of a single session
/// </summary>
public class SessionRecord
{
    public Guid Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ProjectName { get; set; }
    public string? TemplateName { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<string> Achievements { get; set; } = new();
    public List<string> Challenges { get; set; } = new();
}

/// <summary>
/// Context for a specific project
/// </summary>
public class ProjectContext
{
    public string ProjectName { get; set; } = string.Empty;
    public DateTime FirstSeen { get; set; }
    public DateTime LastWorked { get; set; }
    public int TotalSessions { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public string CurrentPhase { get; set; } = "initial";
    public List<string> Goals { get; set; } = new();
    public Dictionary<string, string> Notes { get; set; } = new();
}
