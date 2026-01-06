using Hazina.Tools.Data;
using Hazina.Tools.Models;
using Hazina.Tools.Services.Chat.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.Tools.Services.Chat.Orchestration
{
    /// <summary>
    /// Orchestrates chat with a single LLM that uses function/tool calling
    /// to invoke Data Gathering and Analysis operations only when needed.
    /// This reduces token usage by avoiding multiple LLM calls per message.
    /// </summary>
    public class ChatOrchestrationService
    {
        private readonly IToolExecutor _toolExecutor;
        private readonly OpenAIClientWrapper _llmClient;
        private readonly ILogger<ChatOrchestrationService> _logger;

        public ChatOrchestrationService(
            IToolExecutor toolExecutor,
            OpenAIClientWrapper llmClient,
            ILogger<ChatOrchestrationService> logger)
        {
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Send a message through the orchestrated chat system with tool calling
        /// </summary>
        public async Task<ChatOrchestrationResult> SendMessageAsync(
            string projectId,
            string chatId,
            string userMessage,
            Project project,
            List<ConversationMessage> conversationHistory,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "ChatOrchestration: Processing message for project {ProjectId}, chat {ChatId}",
                projectId,
                chatId);

            var result = new ChatOrchestrationResult
            {
                Success = true,
                TotalTokensUsed = 0,
                ToolCalls = new List<IToolCall>(),
                ToolResults = new List<IToolResult>()
            };

            try
            {
                // 1. Get tool definitions
                var toolDefinitions = _toolExecutor.GetToolDefinitions();

                // 2. TODO: Call LLM with tools (will implement with actual OpenAI SDK integration)
                // For now, this is a placeholder that returns success

                _logger.LogInformation("ChatOrchestration: Processing complete. Tokens used: {Tokens}", result.TotalTokensUsed);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in chat orchestration");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Execute tools requested by the LLM.
        /// STAP 15: Enhanced to detect AwaitingUserResponse and pause orchestration.
        /// </summary>
        private async Task<ToolExecutionResult> ExecuteToolsAsync(
            List<IToolCall> toolCalls,
            string context,
            CancellationToken cancellationToken)
        {
            var executionResult = new ToolExecutionResult
            {
                Results = new List<IToolResult>()
            };

            foreach (var toolCall in toolCalls)
            {
                _logger.LogInformation("Executing tool: {ToolName}", toolCall.FunctionName);

                var result = await _toolExecutor.ExecuteAsync(
                    toolCall.FunctionName,
                    toolCall.Arguments,
                    context,
                    cancellationToken);

                executionResult.Results.Add(result);

                _logger.LogInformation(
                    "Tool {ToolName} executed. Success: {Success}, Tokens: {Tokens}, AwaitingResponse: {Awaiting}",
                    toolCall.FunctionName,
                    result.Success,
                    result.TokensUsed,
                    result.AwaitingUserResponse);

                // STAP 15: Check if tool requires user response
                if (result.AwaitingUserResponse)
                {
                    _logger.LogInformation(
                        "Tool {ToolName} is awaiting user response. Pausing orchestration.",
                        toolCall.FunctionName);

                    executionResult.AwaitingUserResponse = true;
                    executionResult.AwaitingToolResult = result;
                    break; // Stop processing further tools
                }
            }

            return executionResult;
        }
    }

    /// <summary>
    /// Internal result from tool execution batch (STAP 15)
    /// </summary>
    internal class ToolExecutionResult
    {
        public List<IToolResult> Results { get; set; }
        public bool AwaitingUserResponse { get; set; }
        public IToolResult AwaitingToolResult { get; set; }
    }

    /// <summary>
    /// Result from chat orchestration
    /// </summary>
    public class ChatOrchestrationResult
    {
        public bool Success { get; set; }
        public string ResponseMessage { get; set; }
        public string ErrorMessage { get; set; }
        public int TotalTokensUsed { get; set; }
        public List<IToolCall> ToolCalls { get; set; }
        public List<IToolResult> ToolResults { get; set; }

        // STAP 15: Support for async user response pattern
        /// <summary>
        /// Indicates if the orchestration is paused awaiting user response.
        /// When true, the frontend should render interactive components and wait for user input.
        /// </summary>
        public bool AwaitingUserResponse { get; set; }

        /// <summary>
        /// The tool result that triggered the await state (contains guidance/upload request data)
        /// </summary>
        public IToolResult AwaitingToolResult { get; set; }
    }
}
