using Hazina.LLMs;
using Hazina.LLMs.OpenAI;
using Hazina.Tools.AI.Agents;
using Hazina.Tools.Data;
using Hazina.Tools.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AgenticOrchestration.Services.Chat
{
    /// <summary>
    /// Orchestrates LLM-powered chat for Hazina Orchestration with tool calling support
    /// Manages conversations with session-specific context
    /// </summary>
    public class OrchestrationChatService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<OrchestrationChatService> _logger;
        private readonly OpenAIClientWrapper _llmClient;

        // In-memory conversation storage (sessionId -> conversation)
        private readonly ConcurrentDictionary<string, ChatConversation> _conversations = new();

        // Rate limiting (sessionId -> message timestamps)
        private readonly ConcurrentDictionary<string, Queue<DateTime>> _rateLimits = new();
        private const int MAX_MESSAGES_PER_MINUTE = 5;

        // Token management
        private const int MAX_CONVERSATION_MESSAGES = 20; // Sliding window

        // System prompt defining agent identity and capabilities
        private const string SYSTEM_PROMPT = @"You are an AI assistant for the Hazina Terminal Orchestration system.

Your role:
- Help users manage their terminal sessions
- Answer questions about session status and system health
- Provide information about terminal operations
- Execute commands via available tools when needed

Available tools:
- list_sessions: Show all active terminal sessions
- get_session_details: Get detailed info about a specific session
- list_archived_sessions: Show completed/archived sessions
- get_system_status: Show overall system health and session counts
- search_sessions: Find sessions by name or command

Guidelines:
- Be concise and helpful
- Always use tools when user asks about sessions
- Format output clearly (use markdown for better readability)
- If a session ID is mentioned, validate it exists before showing details
- Provide actionable information";

        public OrchestrationChatService(
            IConfiguration configuration,
            ILogger<OrchestrationChatService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize OpenAI client
            var apiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key not configured");
            var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";

            var config = new OpenAIConfig
            {
                Model = model,
                ApiKey = apiKey
            };

            _llmClient = new OpenAIClientWrapper(config, logger);

            _logger.LogInformation("OrchestrationChatService initialized with model {Model}", model);
        }

        /// <summary>
        /// Send a message and get streaming response with tool calling support
        /// </summary>
        public async Task<ChatResponse> SendMessageAsync(
            string sessionId,
            string userMessage,
            IToolsContext toolsContext,
            Action<string>? onChunk = null,
            CancellationToken cancellationToken = default)
        {
            // Check rate limit
            if (!CheckRateLimit(sessionId))
            {
                return new ChatResponse
                {
                    Success = false,
                    ErrorMessage = "Rate limit exceeded. Maximum 5 messages per minute.",
                    TotalTokensUsed = 0
                };
            }

            try
            {
                // Get or create conversation
                var conversation = _conversations.GetOrAdd(sessionId, _ => new ChatConversation
                {
                    MetaData = new ChatMetadata { Id = sessionId, Name = $"Session {sessionId}" },
                    ChatMessages = new SerializableList<ConversationMessage>()
                });

                // Add user message
                conversation.ChatMessages.Add(new ConversationMessage
                {
                    Role = ChatMessageRole.User,
                    Text = userMessage
                });

                // Prune old messages (keep last 20)
                PruneConversation(conversation);

                // Build messages for LLM (system + history)
                var messages = BuildLLMMessages(conversation);

                // Stream response with tool calling
                var responseText = string.Empty;
                var totalTokens = 0;
                var toolCalls = new List<ToolCall>();

                // First LLM call
                var response = await _llmClient.StreamResponseAsync(
                    messages,
                    toolsContext?.GetToolDefinitions() ?? new List<HazinaChatTool>(),
                    chunk =>
                    {
                        responseText += chunk;
                        onChunk?.Invoke(chunk);
                    },
                    cancellationToken);

                totalTokens += response.TokenUsage?.TotalTokens ?? 0;

                // Handle tool calls in a loop (max 5 iterations to prevent infinite loops)
                int maxToolIterations = 5;
                int currentIteration = 0;

                while (response.ToolCalls?.Any() == true && currentIteration < maxToolIterations)
                {
                    currentIteration++;
                    _logger.LogInformation("Processing {Count} tool calls (iteration {Iteration})", response.ToolCalls.Count, currentIteration);

                    foreach (var toolCall in response.ToolCalls)
                    {
                        toolCalls.Add(toolCall);

                        // Execute tool with 30s timeout
                        using var toolCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        toolCts.CancelAfter(TimeSpan.FromSeconds(30));

                        try
                        {
                            var toolResult = await toolsContext.ExecuteAsync(
                                toolCall.FunctionName,
                                toolCall.Arguments,
                                string.Empty, // context
                                toolCts.Token);

                            // Add tool result to messages
                            messages.Add(new HazinaChatMessage
                            {
                                Role = ChatMessageRole.Tool,
                                Content = toolResult.ResultData ?? "Tool executed successfully",
                                ToolCallId = toolCall.Id
                            });

                            _logger.LogInformation("Tool {ToolName} executed successfully", toolCall.FunctionName);
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogWarning("Tool {ToolName} execution timed out after 30s", toolCall.FunctionName);
                            messages.Add(new HazinaChatMessage
                            {
                                Role = ChatMessageRole.Tool,
                                Content = $"Error: Tool execution timed out after 30 seconds",
                                ToolCallId = toolCall.Id
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error executing tool {ToolName}", toolCall.FunctionName);
                            messages.Add(new HazinaChatMessage
                            {
                                Role = ChatMessageRole.Tool,
                                Content = $"Error: {ex.Message}",
                                ToolCallId = toolCall.Id
                            });
                        }
                    }

                    // Call LLM again with tool results
                    response = await _llmClient.StreamResponseAsync(
                        messages,
                        toolsContext?.GetToolDefinitions() ?? new List<HazinaChatTool>(),
                        chunk =>
                        {
                            responseText += chunk;
                            onChunk?.Invoke(chunk);
                        },
                        cancellationToken);

                    totalTokens += response.TokenUsage?.TotalTokens ?? 0;
                }

                // Add assistant response to conversation
                conversation.ChatMessages.Add(new ConversationMessage
                {
                    Role = ChatMessageRole.Assistant,
                    Text = responseText
                });

                return new ChatResponse
                {
                    Success = true,
                    ResponseMessage = responseText,
                    TotalTokensUsed = totalTokens,
                    ToolCalls = toolCalls
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OrchestrationChatService.SendMessageAsync");
                return new ChatResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    TotalTokensUsed = 0
                };
            }
        }

        /// <summary>
        /// Get conversation history for a session
        /// </summary>
        public ChatConversation? GetConversationHistory(string sessionId)
        {
            return _conversations.TryGetValue(sessionId, out var conversation) ? conversation : null;
        }

        /// <summary>
        /// Clear conversation history for a session
        /// </summary>
        public void ClearConversation(string sessionId)
        {
            _conversations.TryRemove(sessionId, out _);
            _logger.LogInformation("Cleared conversation history for session {SessionId}", sessionId);
        }

        /// <summary>
        /// Build LLM messages from conversation (system prompt + history)
        /// </summary>
        private List<HazinaChatMessage> BuildLLMMessages(ChatConversation conversation)
        {
            var messages = new List<HazinaChatMessage>();

            // System prompt
            messages.Add(new HazinaChatMessage
            {
                Role = ChatMessageRole.System,
                Content = SYSTEM_PROMPT
            });

            // Conversation history
            foreach (var msg in conversation.ChatMessages)
            {
                messages.Add(new HazinaChatMessage
                {
                    Role = msg.Role,
                    Content = msg.Text ?? string.Empty
                });
            }

            return messages;
        }

        /// <summary>
        /// Prune conversation to keep only last N messages (prevent token overflow)
        /// </summary>
        private void PruneConversation(ChatConversation conversation)
        {
            if (conversation.ChatMessages.Count > MAX_CONVERSATION_MESSAGES)
            {
                var toRemove = conversation.ChatMessages.Count - MAX_CONVERSATION_MESSAGES;
                for (int i = 0; i < toRemove; i++)
                {
                    conversation.ChatMessages.RemoveAt(0);
                }
                _logger.LogInformation("Pruned {Count} old messages from conversation", toRemove);
            }
        }

        /// <summary>
        /// Check rate limit (5 messages per minute per session)
        /// </summary>
        private bool CheckRateLimit(string sessionId)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddMinutes(-1);

            var times = _rateLimits.GetOrAdd(sessionId, _ => new Queue<DateTime>());

            // Remove old timestamps
            lock (times)
            {
                while (times.Count > 0 && times.Peek() < cutoff)
                {
                    times.Dequeue();
                }

                // Check limit
                if (times.Count >= MAX_MESSAGES_PER_MINUTE)
                {
                    return false;
                }

                times.Enqueue(now);
                return true;
            }
        }
    }

    /// <summary>
    /// Response from chat service
    /// </summary>
    public class ChatResponse
    {
        public bool Success { get; set; }
        public string? ResponseMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public int TotalTokensUsed { get; set; }
        public List<ToolCall>? ToolCalls { get; set; }
    }

    /// <summary>
    /// Tool call record
    /// </summary>
    public class ToolCall
    {
        public string Id { get; set; } = string.Empty;
        public string FunctionName { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
    }
}
