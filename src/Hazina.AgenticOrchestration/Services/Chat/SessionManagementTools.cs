using Hazina.AgenticOrchestration.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AgenticOrchestration.Services.Chat
{
    /// <summary>
    /// Session management tools for Hazina Orchestration chat
    /// Provides LLM with capabilities to query and interact with terminal sessions
    /// </summary>
    public static class SessionManagementTools
    {
        /// <summary>
        /// Create all session management tools for the given session manager
        /// </summary>
        public static List<HazinaChatTool> CreateTools(ITerminalSessionManager sessionManager)
        {
            var tools = new List<HazinaChatTool>();

            // Tool 1: list_sessions
            tools.Add(new HazinaChatTool(
                name: "list_sessions",
                description: "List all active terminal sessions with their status",
                parameters: new List<ChatToolParameter>(),
                execute: async (messages, toolCall, ct) =>
                {
                    var sessions = sessionManager.GetAllSessions();
                    var result = sessions.Select(s => new
                    {
                        Id = s.SessionId,
                        Name = s.Title ?? s.Command,
                        Status = s.IsRunning ? "Running" : "Stopped",
                        Command = s.Command,
                        StartTime = s.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                        Duration = (DateTime.Now - s.StartedAt).ToString(@"hh\:mm\:ss"),
                        WaitingForInput = s.WaitingForInput,
                        ExitCode = s.ExitCode
                    }).ToList();

                    return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                }
            ));

            // Tool 2: get_session_details
            var sessionIdParam = new ChatToolParameter
            {
                Name = "sessionId",
                Description = "The session ID to get details for",
                Type = "string",
                Required = true
            };

            tools.Add(new HazinaChatTool(
                name: "get_session_details",
                description: "Get detailed information about a specific session",
                parameters: new List<ChatToolParameter> { sessionIdParam },
                execute: async (messages, toolCall, ct) =>
                {
                    if (!sessionIdParam.TryGetValue(toolCall, out string sessionId))
                    {
                        return JsonSerializer.Serialize(new { error = "sessionId parameter is required" });
                    }

                    var session = sessionManager.GetSession(sessionId);
                    if (session == null)
                    {
                        return JsonSerializer.Serialize(new { error = $"Session '{sessionId}' not found" });
                    }

                    var result = new
                    {
                        Id = session.SessionId,
                        Name = session.Title ?? session.Command,
                        Status = session.IsRunning ? "Running" : "Stopped",
                        Command = session.Command,
                        StartTime = session.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                        Duration = (DateTime.Now - session.StartedAt).ToString(@"hh\:mm\:ss"),
                        WaitingForInput = session.WaitingForInput,
                        ExitCode = session.ExitCode,
                        Note = "Output retrieval: Use GetOutputHistory() method"
                    };

                    return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                }
            ));

            // Tool 3: list_archived_sessions
            var limitParam = new ChatToolParameter
            {
                Name = "limit",
                Description = "Maximum number of sessions to return (default 20)",
                Type = "number",
                Required = false
            };

            tools.Add(new HazinaChatTool(
                name: "list_archived_sessions",
                description: "List archived/completed sessions",
                parameters: new List<ChatToolParameter> { limitParam },
                execute: async (messages, toolCall, ct) =>
                {
                    // For now, return empty list (archived sessions not yet implemented)
                    return JsonSerializer.Serialize(new
                    {
                        Message = "Archived sessions feature coming soon",
                        ArchivedSessions = new List<object>()
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
            ));

            // Tool 4: get_system_status
            tools.Add(new HazinaChatTool(
                name: "get_system_status",
                description: "Get overall system status including session counts and resource usage",
                parameters: new List<ChatToolParameter>(),
                execute: async (messages, toolCall, ct) =>
                {
                    var allSessions = sessionManager.GetAllSessions().ToList();
                    var activeSessions = allSessions.Where(s => s.IsRunning).ToList();
                    var waitingSessions = allSessions.Where(s => s.WaitingForInput).ToList();

                    var result = new
                    {
                        TotalSessions = allSessions.Count,
                        ActiveSessions = activeSessions.Count,
                        InactiveSessions = allSessions.Count - activeSessions.Count,
                        WaitingSessions = waitingSessions.Count,
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Sessions = allSessions.Select(s => new
                        {
                            Id = s.SessionId,
                            Name = s.Title ?? s.Command,
                            Status = s.IsRunning ? "Running" : "Stopped",
                            Duration = (DateTime.Now - s.StartedAt).ToString(@"hh\:mm\:ss"),
                            WaitingForInput = s.WaitingForInput
                        }).ToList()
                    };

                    return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                }
            ));

            // Tool 5: search_sessions
            var queryParam = new ChatToolParameter
            {
                Name = "query",
                Description = "Search term to match against session name or command",
                Type = "string",
                Required = true
            };

            tools.Add(new HazinaChatTool(
                name: "search_sessions",
                description: "Search sessions by name or command",
                parameters: new List<ChatToolParameter> { queryParam },
                execute: async (messages, toolCall, ct) =>
                {
                    if (!queryParam.TryGetValue(toolCall, out string query))
                    {
                        return JsonSerializer.Serialize(new { error = "query parameter is required" });
                    }

                    var queryLower = query.ToLowerInvariant();
                    var sessions = sessionManager.GetAllSessions()
                        .Where(s =>
                            (s.Title?.ToLowerInvariant().Contains(queryLower) == true) ||
                            (s.Command?.ToLowerInvariant().Contains(queryLower) == true))
                        .Select(s => new
                        {
                            Id = s.SessionId,
                            Name = s.Title ?? s.Command,
                            Status = s.IsRunning ? "Running" : "Stopped",
                            Command = s.Command,
                            StartTime = s.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                            WaitingForInput = s.WaitingForInput
                        })
                        .ToList();

                    var result = new
                    {
                        Query = query,
                        MatchCount = sessions.Count,
                        Matches = sessions
                    };

                    return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                }
            ));

            return tools;
        }
    }
}
