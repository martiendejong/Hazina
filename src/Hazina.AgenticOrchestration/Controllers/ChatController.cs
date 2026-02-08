using Hazina.AgenticOrchestration.Hubs;
using Hazina.AgenticOrchestration.Services.Chat;
using Hazina.AgenticOrchestration.Terminal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AgenticOrchestration.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly OrchestrationChatService _chatService;
        private readonly ITerminalSessionManager _sessionManager;
        private readonly IHubContext<ClaudeOrchestrationHub> _hubContext;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            OrchestrationChatService chatService,
            ITerminalSessionManager sessionManager,
            IHubContext<ClaudeOrchestrationHub> hubContext,
            ILogger<ChatController> logger)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Send a message to the chat and get streaming response
        /// </summary>
        /// <param name="sessionId">Session ID for chat context</param>
        /// <param name="request">Message request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        [HttpPost("{sessionId}/message")]
        public async Task<IActionResult> SendMessage(
            string sessionId,
            [FromBody] ChatMessageRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
            {
                return BadRequest(new { error = "Message cannot be empty" });
            }

            _logger.LogInformation("Chat message received for session {SessionId}: {Message}",
                sessionId, request.Message);

            try
            {
                // Create tools context with session manager
                var toolsContext = new OrchestrationToolsContext(_sessionManager);

                // Stream response via SignalR
                var response = await _chatService.SendMessageAsync(
                    sessionId,
                    request.Message,
                    toolsContext,
                    chunk =>
                    {
                        // Send chunk to SignalR group
                        _ = _hubContext.Clients.Group($"chat-{sessionId}")
                            .SendAsync("ChatChunk", new
                            {
                                sessionId,
                                chunk,
                                timestamp = DateTime.UtcNow
                            }, cancellationToken);
                    },
                    cancellationToken);

                if (!response.Success)
                {
                    return BadRequest(new { error = response.ErrorMessage });
                }

                // Send completion signal
                await _hubContext.Clients.Group($"chat-{sessionId}")
                    .SendAsync("ChatComplete", new
                    {
                        sessionId,
                        message = response.ResponseMessage,
                        tokensUsed = response.TotalTokensUsed,
                        toolCalls = response.ToolCalls?.Count ?? 0,
                        timestamp = DateTime.UtcNow
                    }, cancellationToken);

                return Ok(new
                {
                    success = true,
                    message = response.ResponseMessage,
                    tokensUsed = response.TotalTokensUsed,
                    toolCalls = response.ToolCalls?.Count ?? 0
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Chat request cancelled for session {SessionId}", sessionId);
                return StatusCode(499, new { error = "Request cancelled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat message for session {SessionId}", sessionId);
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        /// <summary>
        /// Get conversation history for a session
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        [HttpGet("{sessionId}/history")]
        public IActionResult GetHistory(string sessionId)
        {
            try
            {
                var conversation = _chatService.GetConversationHistory(sessionId);

                if (conversation == null)
                {
                    return Ok(new
                    {
                        sessionId,
                        messages = Array.Empty<object>()
                    });
                }

                return Ok(new
                {
                    sessionId,
                    messages = conversation.ChatMessages
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving conversation history for session {SessionId}", sessionId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Clear conversation history for a session
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        [HttpDelete("{sessionId}")]
        public IActionResult ClearHistory(string sessionId)
        {
            try
            {
                _chatService.ClearConversation(sessionId);

                return Ok(new
                {
                    success = true,
                    message = "Conversation history cleared"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing conversation history for session {SessionId}", sessionId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Subscribe to chat updates via SignalR
        /// </summary>
        /// <param name="sessionId">Session ID to subscribe to</param>
        [HttpPost("{sessionId}/subscribe")]
        public async Task<IActionResult> Subscribe(string sessionId)
        {
            try
            {
                // This endpoint is mainly for documentation
                // Actual subscription happens on client-side via SignalR connection
                // Client should call: connection.invoke("JoinChatSession", sessionId)

                return Ok(new
                {
                    success = true,
                    message = $"Subscribe to SignalR group 'chat-{sessionId}' for real-time updates",
                    signalRGroup = $"chat-{sessionId}",
                    events = new[]
                    {
                        new { name = "ChatChunk", description = "Streaming chunk received" },
                        new { name = "ChatComplete", description = "Message processing complete" }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in subscribe endpoint");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }

    /// <summary>
    /// Request model for chat message
    /// </summary>
    public class ChatMessageRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}
