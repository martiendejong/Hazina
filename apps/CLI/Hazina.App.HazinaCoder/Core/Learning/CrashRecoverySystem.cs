using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Learning;

/// <summary>
/// Automatic crash detection, conversation persistence, and easy restoration.
/// Ensures no work is lost even if the agent crashes.
/// </summary>
/// <remarks>
/// Improvement #32: Crash Recovery System
///
/// Features:
/// - Auto-save conversation state every N messages
/// - Detect crashes on startup (unclean shutdown)
/// - Offer to restore previous session
/// - Persist working directory, open files, context
/// </remarks>
public class CrashRecoverySystem
{
    private readonly string _recoveryDir;
    private readonly string _currentSessionFile;
    private readonly List<ConversationMessage> _messages = new();
    private SessionState _currentState = new();

    public CrashRecoverySystem(string recoveryDir)
    {
        _recoveryDir = recoveryDir;
        Directory.CreateDirectory(recoveryDir);

        _currentSessionFile = Path.Combine(recoveryDir, "current_session.json");

        // Check for crash on startup
        DetectCrash();
    }

    /// <summary>
    /// Detect if previous session crashed
    /// </summary>
    private void DetectCrash()
    {
        if (File.Exists(_currentSessionFile))
        {
            try
            {
                var json = File.ReadAllText(_currentSessionFile);
                var session = JsonSerializer.Deserialize<RecoverySession>(json);

                if (session != null && !session.CleanShutdown)
                {
                    Console.WriteLine("\n╔════════════════════════════════════════════════════════╗");
                    Console.WriteLine("║  CRASH DETECTED - Previous session did not exit cleanly ║");
                    Console.WriteLine("╚════════════════════════════════════════════════════════╝");
                    Console.WriteLine($"\nLast session: {session.StartTime:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"Messages: {session.Messages.Count}");
                    Console.WriteLine($"Working directory: {session.State.WorkingDirectory}");
                    Console.WriteLine($"Open files: {session.State.OpenFiles.Count}");
                    Console.WriteLine("\nYou can restore the previous session with: /restore-session");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CrashRecovery] Error detecting crash: {ex.Message}");
            }
        }

        // Start new session
        StartNewSession();
    }

    /// <summary>
    /// Start tracking a new session
    /// </summary>
    private void StartNewSession()
    {
        var session = new RecoverySession
        {
            SessionId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow,
            CleanShutdown = false
        };

        SaveSession(session);
    }

    /// <summary>
    /// Record a conversation message
    /// </summary>
    public void RecordMessage(string role, string content)
    {
        _messages.Add(new ConversationMessage
        {
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow
        });

        // Auto-save every 5 messages
        if (_messages.Count % 5 == 0)
        {
            SaveCurrentSession();
        }
    }

    /// <summary>
    /// Update session state
    /// </summary>
    public void UpdateState(string? workingDir = null, List<string>? openFiles = null, string? context = null)
    {
        if (workingDir != null)
            _currentState.WorkingDirectory = workingDir;
        if (openFiles != null)
            _currentState.OpenFiles = openFiles;
        if (context != null)
            _currentState.ContextSummary = context;

        _currentState.LastUpdated = DateTime.UtcNow;

        SaveCurrentSession();
    }

    /// <summary>
    /// Save current session to disk
    /// </summary>
    private void SaveCurrentSession()
    {
        try
        {
            var session = new RecoverySession
            {
                SessionId = Guid.NewGuid(),
                StartTime = DateTime.UtcNow,
                Messages = _messages.ToList(),
                State = _currentState,
                CleanShutdown = false
            };

            SaveSession(session);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CrashRecovery] ERROR saving session: {ex.Message}");
        }
    }

    /// <summary>
    /// Save session to file
    /// </summary>
    private void SaveSession(RecoverySession session)
    {
        var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_currentSessionFile, json);
    }

    /// <summary>
    /// Mark current session as cleanly shutdown
    /// </summary>
    public void MarkCleanShutdown()
    {
        try
        {
            if (File.Exists(_currentSessionFile))
            {
                var json = File.ReadAllText(_currentSessionFile);
                var session = JsonSerializer.Deserialize<RecoverySession>(json);

                if (session != null)
                {
                    session.CleanShutdown = true;
                    session.EndTime = DateTime.UtcNow;
                    SaveSession(session);
                    Console.WriteLine("[CrashRecovery] Session ended cleanly");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CrashRecovery] ERROR marking clean shutdown: {ex.Message}");
        }
    }

    /// <summary>
    /// Restore previous session
    /// </summary>
    public RecoverySession? RestoreSession()
    {
        try
        {
            if (File.Exists(_currentSessionFile))
            {
                var json = File.ReadAllText(_currentSessionFile);
                var session = JsonSerializer.Deserialize<RecoverySession>(json);

                if (session != null)
                {
                    Console.WriteLine($"[CrashRecovery] Restored session from {session.StartTime:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"[CrashRecovery] {session.Messages.Count} messages restored");
                    return session;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CrashRecovery] ERROR restoring session: {ex.Message}");
        }

        return null;
    }
}

public class RecoverySession
{
    public Guid SessionId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool CleanShutdown { get; set; }
    public List<ConversationMessage> Messages { get; set; } = new();
    public SessionState State { get; set; } = new();
}

public class ConversationMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class SessionState
{
    public string WorkingDirectory { get; set; } = string.Empty;
    public List<string> OpenFiles { get; set; } = new();
    public string ContextSummary { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}
