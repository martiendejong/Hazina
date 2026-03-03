using Hazina.AgenticOrchestration.Terminal;
using Hazina.LLMs;
using System;
using System.Collections.Generic;

namespace Hazina.AgenticOrchestration.Services.Chat
{
    /// <summary>
    /// Minimal IToolsContext implementation for Hazina Orchestration chat
    /// Provides session management tools to the LLM
    /// </summary>
    public class OrchestrationToolsContext : IToolsContext
    {
        private readonly ITerminalSessionManager _sessionManager;

        public List<HazinaChatTool> Tools { get; set; }
        public Action<string, string, string>? SendMessage { get; set; }
        public string? ProjectId { get; set; }
        public Action<string, int, int, string>? OnTokensUsed { get; set; }
        public Action<string, string, int>? OnToolExecuted { get; set; }
        public Func<string, int, bool>? ShouldContinue { get; set; }
        public string? ContinuationPrompt { get; set; }
        public int MaxContinuations { get; set; } = 5;

        public OrchestrationToolsContext(ITerminalSessionManager sessionManager)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));

            // Create all session management tools
            Tools = SessionManagementTools.CreateTools(sessionManager);
        }

        public void Add(HazinaChatTool info)
        {
            Tools.Add(info);
        }
    }
}
