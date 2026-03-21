using Hazina.AI.LocalLLM;
using Hazina.EventSourcing;
using Hazina.EventSourcing.Events;
using Hazina.Indexing.LocalSystem;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hazina.AgenticOrchestration.Controllers;

/// <summary>
/// Controller for Local Agent Platform - handles intent parsing, task management, and file indexing
/// </summary>
[ApiController]
[Route("api/agent")]
public class LocalAgentController : ControllerBase
{
    private readonly IIntentParser _intentParser;
    private readonly IEventStore _eventStore;
    private readonly IStructuralIndexer _structuralIndexer;
    private readonly ILogger<LocalAgentController> _logger;

    public LocalAgentController(
        IIntentParser intentParser,
        IEventStore eventStore,
        IStructuralIndexer structuralIndexer,
        ILogger<LocalAgentController> logger)
    {
        _intentParser = intentParser;
        _eventStore = eventStore;
        _structuralIndexer = structuralIndexer;
        _logger = logger;
    }

    /// <summary>
    /// Submit user intent for parsing and execution
    /// POST /api/agent/intent
    /// </summary>
    [HttpPost("intent")]
    public async Task<IActionResult> SubmitIntent([FromBody] SubmitIntentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserInput))
        {
            return BadRequest(new { error = "UserInput is required" });
        }

        try
        {
            var intentId = Guid.NewGuid().ToString();
            var timestamp = DateTime.UtcNow;

            // Store IntentReceivedEvent
            var intentReceivedEvent = new IntentReceivedEvent(
                IntentId: intentId,
                UserInput: request.UserInput,
                Timestamp: timestamp
            );
            await _eventStore.AppendEventAsync(
                aggregateId: intentId,
                aggregateType: "Intent",
                eventType: nameof(IntentReceivedEvent),
                eventData: intentReceivedEvent
            );

            // Parse intent
            var parsedIntent = await _intentParser.ParseAsync(request.UserInput);

            if (parsedIntent == null)
            {
                return Ok(new SubmitIntentResponse
                {
                    IntentId = intentId,
                    Parsed = null,
                    Timestamp = timestamp.ToString("O"),
                    Message = "Failed to parse intent"
                });
            }

            // Store IntentParsedEvent
            var intentParsedEvent = new IntentParsedEvent(
                IntentId: intentId,
                IntentType: parsedIntent.Type,
                TargetEntity: parsedIntent.Target,
                Filters: parsedIntent.Filters,
                Confidence: parsedIntent.Confidence,
                Timestamp: DateTime.UtcNow
            );
            await _eventStore.AppendEventAsync(
                aggregateId: intentId,
                aggregateType: "Intent",
                eventType: nameof(IntentParsedEvent),
                eventData: intentParsedEvent
            );

            // Create task if confidence is high enough
            if (parsedIntent.Confidence >= 0.8)
            {
                var taskId = Guid.NewGuid().ToString();
                var taskCreatedEvent = new TaskCreatedEvent(
                    TaskId: taskId,
                    IntentId: intentId,
                    TaskType: parsedIntent.Type,
                    Parameters: parsedIntent.Filters ?? new Dictionary<string, object>(),
                    Timestamp: DateTime.UtcNow
                );
                await _eventStore.AppendEventAsync(
                    aggregateId: taskId,
                    aggregateType: "Task",
                    eventType: nameof(TaskCreatedEvent),
                    eventData: taskCreatedEvent
                );
            }

            return Ok(new SubmitIntentResponse
            {
                IntentId = intentId,
                Parsed = parsedIntent,
                Timestamp = timestamp.ToString("O"),
                Message = "Intent processed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit intent");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all events from event store
    /// GET /api/agent/events
    /// </summary>
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        [FromQuery] string? aggregateId = null)
    {
        try
        {
            var events = await _eventStore.GetEventsAsync(
                aggregateId: aggregateId,
                skip: offset,
                take: limit
            );

            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get events");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get file tree from structural indexer
    /// GET /api/agent/files
    /// </summary>
    [HttpGet("files")]
    public async Task<IActionResult> GetFileTree([FromQuery] string? rootPath = null)
    {
        try
        {
            var path = rootPath ?? Directory.GetCurrentDirectory();
            var index = await _structuralIndexer.IndexAsync(path);

            return Ok(new
            {
                rootPath = path,
                fileCount = index.Files.Count,
                directoryCount = index.Directories.Count,
                tree = index
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file tree");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all tasks
    /// GET /api/agent/tasks
    /// </summary>
    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasks()
    {
        try
        {
            // Get all task-related events
            var events = await _eventStore.GetEventsAsync(
                aggregateType: "Task",
                skip: 0,
                take: 100
            );

            // Group by task ID and build task info
            var tasks = events
                .GroupBy(e => e.AggregateId)
                .Select(g => BuildTaskInfo(g.ToList()))
                .Where(t => t != null)
                .ToList();

            return Ok(new { tasks });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tasks");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Approve or deny an action
    /// POST /api/agent/approve
    /// </summary>
    [HttpPost("approve")]
    public async Task<IActionResult> ApproveAction([FromBody] ApproveActionRequest request)
    {
        try
        {
            if (request.Approved)
            {
                var approvalGrantedEvent = new ApprovalGrantedEvent(
                    ApprovalId: request.RequestId,
                    TaskId: request.TaskId ?? request.RequestId,
                    ActionType: "approval",
                    ActionTarget: null,
                    Timestamp: DateTime.UtcNow
                );
                await _eventStore.AppendEventAsync(
                    aggregateId: request.RequestId,
                    aggregateType: "Approval",
                    eventType: nameof(ApprovalGrantedEvent),
                    eventData: approvalGrantedEvent
                );
            }
            else
            {
                var approvalDeniedEvent = new ApprovalDeniedEvent(
                    ApprovalId: request.RequestId,
                    TaskId: request.TaskId ?? request.RequestId,
                    ActionType: "approval",
                    Reason: request.Reason,
                    Timestamp: DateTime.UtcNow
                );
                await _eventStore.AppendEventAsync(
                    aggregateId: request.RequestId,
                    aggregateType: "Approval",
                    eventType: nameof(ApprovalDeniedEvent),
                    eventData: approvalDeniedEvent
                );
            }

            return Ok(new { success = true, message = request.Approved ? "Approved" : "Denied" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve action");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// GET /api/health
    /// </summary>
    [HttpGet("/api/health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    private TaskInfo? BuildTaskInfo(List<StoredEvent> events)
    {
        if (events.Count == 0) return null;

        var taskId = events[0].AggregateId;
        var createdEvent = events.FirstOrDefault(e => e.EventType == nameof(TaskCreatedEvent));
        if (createdEvent == null) return null;

        var status = "pending";
        if (events.Any(e => e.EventType == nameof(TaskStartedEvent)))
            status = "running";
        if (events.Any(e => e.EventType == nameof(TaskCompletedEvent)))
            status = "completed";
        if (events.Any(e => e.EventType == nameof(TaskFailedEvent)))
            status = "failed";

        return new TaskInfo
        {
            TaskId = taskId,
            IntentId = taskId, // TODO: extract from event data
            TaskType = "unknown", // TODO: extract from event data
            Status = status,
            Parameters = new Dictionary<string, object>(),
            CreatedAt = createdEvent.Timestamp.ToString("O"),
            UpdatedAt = events.Max(e => e.Timestamp).ToString("O")
        };
    }
}

// Request/Response DTOs
public record SubmitIntentRequest
{
    public required string UserInput { get; init; }
}

public record SubmitIntentResponse
{
    public required string IntentId { get; init; }
    public ParsedIntent? Parsed { get; init; }
    public required string Timestamp { get; init; }
    public string? Message { get; init; }
}

public record ApproveActionRequest
{
    public required string RequestId { get; init; }
    public bool Approved { get; init; }
    public string? TaskId { get; init; }
    public string? Reason { get; init; }
}

public record TaskInfo
{
    public required string TaskId { get; init; }
    public required string IntentId { get; init; }
    public required string TaskType { get; init; }
    public required string Status { get; init; }
    public required Dictionary<string, object> Parameters { get; init; }
    public required string CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
    public object? Result { get; init; }
    public string? Error { get; init; }
}
