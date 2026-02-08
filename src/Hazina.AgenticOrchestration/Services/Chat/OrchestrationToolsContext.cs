using Hazina.AgenticOrchestration.Terminal;
using Hazina.LLMs;
using Hazina.Tools.AI.Agents;
using Hazina.Tools.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AgenticOrchestration.Services.Chat
{
    /// <summary>
    /// Tools context for Hazina Orchestration chat
    /// Provides access to terminal session management tools
    /// </summary>
    public class OrchestrationToolsContext : IToolsContext
    {
        public ITerminalSessionManager SessionManager { get; set; } = null!;
        public List<HazinaChatTool> Tools { get; set; } = new();
        public Action<string, string, string>? SendMessage { get; set; }
        public string? ProjectId { get; set; }
        public Action<string, int, int, string>? OnTokensUsed { get; set; }
        public Action<string, string, int>? OnToolExecuted { get; set; }
        public Func<string, int, bool>? ShouldContinue { get; set; }
        public string? ContinuationPrompt { get; set; }
        public int MaxContinuations { get; set; } = 5;

        public OrchestrationToolsContext(ITerminalSessionManager sessionManager)
        {
            SessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));

            // Register session management tools
            this.AddSessionManagementTools(sessionManager);
        }

        public void Add(HazinaChatTool info)
        {
            Tools.Add(info);
        }

        /// <summary>
        /// Get all tool definitions for LLM
        /// </summary>
        public List<HazinaChatTool> GetToolDefinitions()
        {
            return Tools;
        }

        /// <summary>
        /// Execute a tool by name with arguments
        /// </summary>
        public async Task<ToolExecutionResult> ExecuteAsync(string toolName, string arguments, string context, CancellationToken cancellationToken)
        {
            var tool = Tools.FirstOrDefault(t => t.Name == toolName);

            if (tool == null)
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    ResultData = $"Tool '{toolName}' not found"
                };
            }

            try
            {
                return await tool.ExecuteAsync(arguments, this, cancellationToken);
            }
            catch (Exception ex)
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    ResultData = $"Tool execution failed: {ex.Message}"
                };
            }
        }
    }

    /// <summary>
    /// Tool execution result for Orchestration
    /// </summary>
    public class ToolExecutionResult
    {
        public bool Success { get; set; }
        public string ResultData { get; set; } = string.Empty;
        public int TokensUsed { get; set; }
    }
}
