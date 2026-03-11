using Hazina.AgenticOrchestration.Services;
using Hazina.AgenticOrchestration.Services.Chat;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hazina.AgenticOrchestration.Controllers;

/// <summary>
/// Admin endpoints for chat service monitoring and management
/// </summary>
[ApiController]
[Route("api/chat/admin")]
public class ChatAdminController : ControllerBase
{
    private readonly EnterpriseOrchestrationChatService _chatService;
    private readonly ConversationRepository _repository;
    private readonly ISessionOrderingService _orderingService;
    private readonly ILogger<ChatAdminController> _logger;

    public ChatAdminController(
        EnterpriseOrchestrationChatService chatService,
        ConversationRepository repository,
        ISessionOrderingService orderingService,
        ILogger<ChatAdminController> logger)
    {
        _chatService = chatService;
        _repository = repository;
        _orderingService = orderingService;
        _logger = logger;
    }

    /// <summary>
    /// Get service health status
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        try
        {
            var health = await _chatService.GetHealthAsync();
            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get health status");
            return StatusCode(500, new { error = "Failed to get health status" });
        }
    }

    /// <summary>
    /// Get metrics for specific session
    /// </summary>
    [HttpGet("metrics/{sessionId}")]
    public IActionResult GetSessionMetrics(string sessionId)
    {
        try
        {
            var metrics = _chatService.GetMetrics(sessionId);

            if (metrics == null)
            {
                return NotFound(new { error = $"No metrics found for session {sessionId}" });
            }

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metrics for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to get metrics" });
        }
    }

    /// <summary>
    /// Get metrics for all sessions
    /// </summary>
    [HttpGet("metrics")]
    public IActionResult GetAllMetrics()
    {
        try
        {
            var metrics = _chatService.GetAllMetrics();
            return Ok(new
            {
                totalSessions = metrics.Count,
                totalMessages = metrics.Values.Sum(m => m.MessageCount),
                totalTokens = metrics.Values.Sum(m => m.TotalTokens),
                totalToolCalls = metrics.Values.Sum(m => m.TotalToolCalls),
                averageDuration = metrics.Values.Any()
                    ? metrics.Values.Average(m => m.AverageDurationMs)
                    : 0,
                sessions = metrics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all metrics");
            return StatusCode(500, new { error = "Failed to get metrics" });
        }
    }

    /// <summary>
    /// List all saved conversations
    /// </summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> ListConversations(CancellationToken ct)
    {
        try
        {
            var conversations = await _repository.ListAsync(ct);
            return Ok(new
            {
                count = conversations.Count,
                totalSize = _repository.GetTotalSizeBytes(),
                totalSizeMB = _repository.GetTotalSizeBytes() / 1024.0 / 1024.0,
                conversations
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list conversations");
            return StatusCode(500, new { error = "Failed to list conversations" });
        }
    }

    /// <summary>
    /// Export conversation to file
    /// </summary>
    [HttpPost("conversations/{sessionId}/export")]
    public async Task<IActionResult> ExportConversation(
        string sessionId,
        [FromBody] ExportRequest request,
        CancellationToken ct)
    {
        try
        {
            var exportPath = request.ExportPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "HazinaOrchestration",
                "ChatExports");

            var filePath = await _chatService.ExportConversationAsync(sessionId, exportPath, ct);

            return Ok(new
            {
                success = true,
                filePath,
                message = $"Conversation exported to {filePath}"
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export conversation {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to export conversation" });
        }
    }

    /// <summary>
    /// Delete conversation
    /// </summary>
    [HttpDelete("conversations/{sessionId}")]
    public async Task<IActionResult> DeleteConversation(string sessionId, CancellationToken ct)
    {
        try
        {
            await _chatService.ClearHistoryAsync(sessionId, ct);

            return Ok(new
            {
                success = true,
                message = $"Conversation {sessionId} deleted"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete conversation {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to delete conversation" });
        }
    }

    /// <summary>
    /// Clean up old conversations
    /// </summary>
    [HttpPost("conversations/cleanup")]
    public async Task<IActionResult> CleanupOldConversations(
        [FromBody] CleanupRequest request,
        CancellationToken ct)
    {
        try
        {
            var olderThanDays = request.OlderThanDays ?? 30;

            await _repository.CleanupOldAsync(olderThanDays, ct);

            return Ok(new
            {
                success = true,
                message = $"Cleaned up conversations older than {olderThanDays} days"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old conversations");
            return StatusCode(500, new { error = "Failed to cleanup conversations" });
        }
    }

    /// <summary>
    /// Get conversation statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStatistics(CancellationToken ct)
    {
        try
        {
            var conversations = await _repository.ListAsync(ct);
            var metrics = _chatService.GetAllMetrics();
            var health = await _chatService.GetHealthAsync();

            var stats = new
            {
                overview = new
                {
                    activeConversations = health.ActiveConversations,
                    savedConversations = conversations.Count,
                    totalMessages = health.TotalMessages,
                    totalTokens = health.TotalTokens,
                    totalToolCalls = health.TotalToolCalls,
                    totalStorageMB = health.TotalStorageMB,
                    circuitBreakerState = health.CircuitBreakerState
                },
                topSessions = metrics
                    .OrderByDescending(kvp => kvp.Value.MessageCount)
                    .Take(10)
                    .Select(kvp => new
                    {
                        sessionId = kvp.Key,
                        messages = kvp.Value.MessageCount,
                        tokens = kvp.Value.TotalTokens,
                        tools = kvp.Value.TotalToolCalls,
                        avgDuration = kvp.Value.AverageDurationMs,
                        lastMessage = kvp.Value.LastMessageAt
                    }),
                recentActivity = metrics
                    .OrderByDescending(kvp => kvp.Value.LastMessageAt)
                    .Take(10)
                    .Select(kvp => new
                    {
                        sessionId = kvp.Key,
                        lastMessage = kvp.Value.LastMessageAt,
                        messages = kvp.Value.MessageCount
                    }),
                tokenUsage = new
                {
                    total = metrics.Values.Sum(m => m.TotalTokens),
                    average = metrics.Values.Any()
                        ? metrics.Values.Average(m => m.TotalTokens)
                        : 0,
                    max = metrics.Values.Any()
                        ? metrics.Values.Max(m => m.TotalTokens)
                        : 0
                },
                toolUsage = new
                {
                    total = metrics.Values.Sum(m => m.TotalToolCalls),
                    average = metrics.Values.Any()
                        ? metrics.Values.Average(m => m.TotalToolCalls)
                        : 0,
                    sessionsWithTools = metrics.Values.Count(m => m.TotalToolCalls > 0)
                },
                performance = new
                {
                    averageDurationMs = metrics.Values.Any()
                        ? metrics.Values.Average(m => m.AverageDurationMs)
                        : 0,
                    maxDurationMs = metrics.Values.Any()
                        ? metrics.Values.Max(m => m.AverageDurationMs)
                        : 0,
                    minDurationMs = metrics.Values.Any()
                        ? metrics.Values.Min(m => m.AverageDurationMs)
                        : 0
                }
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get statistics");
            return StatusCode(500, new { error = "Failed to get statistics" });
        }
    }

    /// <summary>
    /// Get system configuration
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfiguration()
    {
        try
        {
            return Ok(new
            {
                maxMessagesPerMinute = 10,
                maxMessagesPerHour = 60,
                maxConversationMessages = 30,
                autoSaveIntervalSeconds = 30,
                retryAttempts = 3,
                circuitBreakerFailureThreshold = 3,
                circuitBreakerDurationSeconds = 30
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get configuration");
            return StatusCode(500, new { error = "Failed to get configuration" });
        }
    }

    /// <summary>
    /// Update session display ordering
    /// </summary>
    [HttpPut("sessions/reorder")]
    public async Task<IActionResult> ReorderSessions(
        [FromBody] ReorderRequest request,
        CancellationToken ct)
    {
        try
        {
            if (request.Sessions == null || request.Sessions.Count == 0)
            {
                return BadRequest(new { error = "Sessions array is required" });
            }

            // Build ordering dictionary from request
            var ordering = new Dictionary<string, int>();
            foreach (var session in request.Sessions)
            {
                ordering[session.SessionId] = session.Order;
            }

            await _orderingService.SaveOrderingAsync(ordering);

            return Ok(new
            {
                success = true,
                message = $"Updated ordering for {ordering.Count} sessions"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reorder sessions");
            return StatusCode(500, new { error = "Failed to reorder sessions" });
        }
    }

    /// <summary>
    /// Get session ordering
    /// </summary>
    [HttpGet("sessions/ordering")]
    public async Task<IActionResult> GetSessionOrdering(CancellationToken ct)
    {
        try
        {
            var ordering = await _orderingService.GetOrderingAsync();
            return Ok(new
            {
                ordering
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session ordering");
            return StatusCode(500, new { error = "Failed to get session ordering" });
        }
    }
}

public class ExportRequest
{
    public string? ExportPath { get; set; }
}

public class CleanupRequest
{
    public int? OlderThanDays { get; set; }
}

public class ReorderRequest
{
    public List<SessionOrder> Sessions { get; set; } = new();
}

public class SessionOrder
{
    public string SessionId { get; set; } = string.Empty;
    public int Order { get; set; }
}
