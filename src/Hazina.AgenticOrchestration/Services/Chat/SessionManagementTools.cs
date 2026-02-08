using Hazina.AgenticOrchestration.Terminal;
using Hazina.LLMs;
using Hazina.Tools.AI.Agents;
using Hazina.Tools.Data;
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
        /// Register all session management tools with the tools context
        /// </summary>
        public static void AddSessionManagementTools(this IToolsContext context, ITerminalSessionManager sessionManager)
        {
            if (context is not OrchestrationToolsContext orchContext)
            {
                throw new ArgumentException("Context must be OrchestrationToolsContext", nameof(context));
            }

            orchContext.SessionManager = sessionManager;

            // Register tools
            orchContext.Tools.Add(CreateListSessionsTool());
            orchContext.Tools.Add(CreateGetSessionDetailsTool());
            orchContext.Tools.Add(CreateListArchivedSessionsTool());
            orchContext.Tools.Add(CreateGetSystemStatusTool());
            orchContext.Tools.Add(CreateSearchSessionsTool());
        }

        private static HazinaChatTool CreateListSessionsTool()
        {
            return new HazinaChatTool
            {
                Name = "list_sessions",
                Description = "List all active terminal sessions with their status",
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>(),
                    ["required"] = new string[0]
                },
                ExecuteAsync = async (args, context, ct) =>
                {
                    if (context is not OrchestrationToolsContext orchContext)
                    {
                        return new ToolExecutionResult
                        {
                            Success = false,
                            ResultData = "Invalid context type"
                        };
                    }

                    var sessions = orchContext.SessionManager.GetAllSessions();
                    var result = sessions.Select(s => new
                    {
                        Id = s.SessionId,
                        Name = s.Config.Name,
                        Status = s.IsActive ? "Running" : "Stopped",
                        Command = s.Config.Command,
                        WorkingDirectory = s.Config.WorkingDirectory,
                        StartTime = s.Config.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown",
                        Duration = s.Config.StartTime.HasValue
                            ? (DateTime.Now - s.Config.StartTime.Value).ToString(@"hh\:mm\:ss")
                            : "Unknown"
                    }).ToList();

                    return new ToolExecutionResult
                    {
                        Success = true,
                        ResultData = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                    };
                }
            };
        }

        private static HazinaChatTool CreateGetSessionDetailsTool()
        {
            return new HazinaChatTool
            {
                Name = "get_session_details",
                Description = "Get detailed information about a specific session",
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["sessionId"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "The session ID to get details for"
                        }
                    },
                    ["required"] = new[] { "sessionId" }
                },
                ExecuteAsync = async (args, context, ct) =>
                {
                    if (context is not OrchestrationToolsContext orchContext)
                    {
                        return new ToolExecutionResult
                        {
                            Success = false,
                            ResultData = "Invalid context type"
                        };
                    }

                    var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(args);
                    var sessionId = parameters?["sessionId"];

                    if (string.IsNullOrEmpty(sessionId))
                    {
                        return new ToolExecutionResult
                        {
                            Success = false,
                            ResultData = "sessionId parameter is required"
                        };
                    }

                    var session = orchContext.SessionManager.GetSession(sessionId);
                    if (session == null)
                    {
                        return new ToolExecutionResult
                        {
                            Success = false,
                            ResultData = $"Session '{sessionId}' not found"
                        };
                    }

                    var result = new
                    {
                        Id = session.SessionId,
                        Name = session.Config.Name,
                        Status = session.IsActive ? "Running" : "Stopped",
                        Command = session.Config.Command,
                        Arguments = session.Config.Arguments,
                        WorkingDirectory = session.Config.WorkingDirectory,
                        StartTime = session.Config.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown",
                        Duration = session.Config.StartTime.HasValue
                            ? (DateTime.Now - session.Config.StartTime.Value).ToString(@"hh\:mm\:ss")
                            : "Unknown",
                        Columns = session.Config.Columns,
                        Rows = session.Config.Rows,
                        RecentOutput = GetRecentOutput(session, 50)
                    };

                    return new ToolExecutionResult
                    {
                        Success = true,
                        ResultData = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                    };
                }
            };
        }

        private static HazinaChatTool CreateListArchivedSessionsTool()
        {
            return new HazinaChatTool
            {
                Name = "list_archived_sessions",
                Description = "List archived/completed sessions",
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["limit"] = new Dictionary<string, object>
                        {
                            ["type"] = "number",
                            ["description"] = "Maximum number of sessions to return (default 20)"
                        }
                    },
                    ["required"] = new string[0]
                },
                ExecuteAsync = async (args, context, ct) =>
                {
                    // For now, return empty list (archived sessions not yet implemented)
                    // This can be enhanced later with session persistence
                    return new ToolExecutionResult
                    {
                        Success = true,
                        ResultData = JsonSerializer.Serialize(new
                        {
                            Message = "Archived sessions feature coming soon",
                            ArchivedSessions = new List<object>()
                        }, new JsonSerializerOptions { WriteIndented = true })
                    };
                }
            };
        }

        private static HazinaChatTool CreateGetSystemStatusTool()
        {
            return new HazinaChatTool
            {
                Name = "get_system_status",
                Description = "Get overall system status including session counts and resource usage",
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>(),
                    ["required"] = new string[0]
                },
                ExecuteAsync = async (args, context, ct) =>
                {
                    if (context is not OrchestrationToolsContext orchContext)
                    {
                        return new ToolExecutionResult
                        {
                            Success = false,
                            ResultData = "Invalid context type"
                        };
                    }

                    var allSessions = orchContext.SessionManager.GetAllSessions().ToList();
                    var activeSessions = allSessions.Where(s => s.IsActive).ToList();

                    var result = new
                    {
                        TotalSessions = allSessions.Count,
                        ActiveSessions = activeSessions.Count,
                        InactiveSessions = allSessions.Count - activeSessions.Count,
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Sessions = allSessions.Select(s => new
                        {
                            Id = s.SessionId,
                            Name = s.Config.Name,
                            Status = s.IsActive ? "Running" : "Stopped",
                            Duration = s.Config.StartTime.HasValue
                                ? (DateTime.Now - s.Config.StartTime.Value).ToString(@"hh\:mm\:ss")
                                : "Unknown"
                        }).ToList()
                    };

                    return new ToolExecutionResult
                    {
                        Success = true,
                        ResultData = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                    };
                }
            };
        }

        private static HazinaChatTool CreateSearchSessionsTool()
        {
            return new HazinaChatTool
            {
                Name = "search_sessions",
                Description = "Search sessions by name or command",
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["query"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Search term to match against session name or command"
                        }
                    },
                    ["required"] = new[] { "query" }
                },
                ExecuteAsync = async (args, context, ct) =>
                {
                    if (context is not OrchestrationToolsContext orchContext)
                    {
                        return new ToolExecutionResult
                        {
                            Success = false,
                            ResultData = "Invalid context type"
                        };
                    }

                    var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(args);
                    var query = parameters?["query"]?.ToLowerInvariant();

                    if (string.IsNullOrEmpty(query))
                    {
                        return new ToolExecutionResult
                        {
                            Success = false,
                            ResultData = "query parameter is required"
                        };
                    }

                    var sessions = orchContext.SessionManager.GetAllSessions()
                        .Where(s =>
                            (s.Config.Name?.ToLowerInvariant().Contains(query) == true) ||
                            (s.Config.Command?.ToLowerInvariant().Contains(query) == true))
                        .Select(s => new
                        {
                            Id = s.SessionId,
                            Name = s.Config.Name,
                            Status = s.IsActive ? "Running" : "Stopped",
                            Command = s.Config.Command,
                            WorkingDirectory = s.Config.WorkingDirectory,
                            StartTime = s.Config.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"
                        })
                        .ToList();

                    return new ToolExecutionResult
                    {
                        Success = true,
                        ResultData = JsonSerializer.Serialize(new
                        {
                            Query = query,
                            MatchCount = sessions.Count,
                            Matches = sessions
                        }, new JsonSerializerOptions { WriteIndented = true })
                    };
                }
            };
        }

        /// <summary>
        /// Get recent output from a session (helper method)
        /// </summary>
        private static string GetRecentOutput(ITerminalSession session, int lineCount)
        {
            try
            {
                // Try to get output buffer if available
                // This is a placeholder - actual implementation depends on session type
                return "(Output capture feature coming soon)";
            }
            catch
            {
                return "(Unable to retrieve output)";
            }
        }
    }
}
