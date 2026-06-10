# Tool System Enhancements - Batch 5

This document describes the enhancements made to Hazina's tool system in Batch 5.

## Overview

Batch 5 adds two major capabilities to the tool system:
1. **Structured Logging with Correlation Tracking**
2. **Message Enricher Pipeline**

These enhancements complement the existing tool provider system (PR #244) to provide complete observability and message transformation capabilities.

## 1. Structured Logging + Correlation (Task 869cabf4g)

### Components

#### ToolExecutionContext
Record type that tracks correlation across tool executions.

**Key Properties:**
- `ExecutionId`: Unique ID for this execution
- `CorrelationId`: Groups related executions (uses W3C Trace Context)
- `ParentId`: Links nested tool calls
- `TraceId` / `SpanId`: Distributed tracing integration
- `UserId`, `SessionId`: Business context
- `Metadata`: Custom execution data

**Usage:**
```csharp
var context = new ToolExecutionContext
{
    ToolName = "search",
    UserId = "user123",
    SessionId = "session456"
};

// Create child context for nested calls
var childContext = context.CreateChild("analyze");

// Extract correlation properties for logging
var properties = context.GetCorrelationProperties();
```

#### ToolExecutionLogger
Structured logger with built-in correlation support.

**Features:**
- Automatic correlation scope management
- Start/Success/Failure logging with timing
- Validation failure tracking
- Integration with `Activity` for distributed tracing
- ILogger pattern compatible

**Usage:**
```csharp
var logger = loggerFactory.CreateLogger<MyTool>();
var toolLogger = logger.ForToolExecution();

// Log execution lifecycle
toolLogger.LogExecutionStart(context, arguments);

try
{
    var result = await ExecuteTool();
    toolLogger.LogExecutionSuccess(context, duration, result);
}
catch (Exception ex)
{
    toolLogger.LogExecutionFailure(context, ex, duration);
}

// Custom events with correlation
toolLogger.LogEvent(context, LogLevel.Information, "Custom event: {Value}", value);
```

**Distributed Tracing:**
```csharp
using var activity = ToolExecutionLogger.StartActivity(
    context,
    activitySource,
    ActivityKind.Internal);

// Activity is automatically tagged with:
// - tool.name
// - tool.execution_id
// - tool.correlation_id
// - tool.user_id
// - tool.session_id
```

#### ToolExecutionMetrics
In-memory metrics collector for tool performance analysis.

**Metrics Tracked:**
- Success/failure counts
- Duration statistics (min, max, average, percentiles)
- Error type distribution
- First/last execution timestamps

**Usage:**
```csharp
var metrics = new ToolExecutionMetrics();

// Record executions
metrics.RecordSuccess("search", TimeSpan.FromMilliseconds(150));
metrics.RecordFailure("analyze", TimeSpan.FromSeconds(2), "TimeoutException");
metrics.RecordValidationFailure("invalid_tool");

// Get metrics
var toolMetrics = metrics.GetMetrics("search");
Console.WriteLine($"Success rate: {toolMetrics.SuccessRate:P2}");
Console.WriteLine($"P95 duration: {toolMetrics.GetPercentileStats().P95}");

// Get summary
var summary = metrics.GetSummary();
Console.WriteLine($"Total tools: {summary.TotalTools}");
Console.WriteLine($"Total executions: {summary.TotalExecutions}");
```

### Integration Example

```csharp
// Setup
var activitySource = new ActivitySource("Hazina.Tools");
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<ToolExecutor>();
var toolLogger = logger.ForToolExecution();
var metrics = new ToolExecutionMetrics();

// Execute tool with full observability
var context = new ToolExecutionContext
{
    ToolName = "search",
    UserId = "user123"
};

using var activity = ToolExecutionLogger.StartActivity(context, activitySource);
var stopwatch = Stopwatch.StartNew();

toolLogger.LogExecutionStart(context, arguments);

try
{
    var result = await tool.Execute(messages, toolCall, cancellationToken);

    stopwatch.Stop();
    toolLogger.LogExecutionSuccess(context, stopwatch.Elapsed, result);
    metrics.RecordSuccess(context.ToolName!, stopwatch.Elapsed);

    return result;
}
catch (Exception ex)
{
    stopwatch.Stop();
    toolLogger.LogExecutionFailure(context, ex, stopwatch.Elapsed);
    metrics.RecordFailure(context.ToolName!, stopwatch.Elapsed, ex.GetType().Name);
    throw;
}
```

## 2. Message Enricher Pipeline (Task 869cabf42)

### Components

#### IMessageEnricher
Interface for message transformation middleware.

**Contract:**
```csharp
public interface IMessageEnricher
{
    string Name { get; }
    int Priority { get; }  // Lower values run first

    Task<List<HazinaChatMessage>> EnrichAsync(
        List<HazinaChatMessage> messages,
        EnrichmentContext context,
        CancellationToken cancellationToken = default);
}
```

#### EnrichmentContext
Metadata container for enrichment pipeline.

**Properties:**
- `ConversationId`: Track conversation
- `UserId`: User context
- `Metadata`: Session metadata
- `Timestamp`: Request time
- `SkipEnrichers`: Opt-out of specific enrichers
- `EnricherData`: Shared data between enrichers

#### MessageEnricherPipeline
Orchestrates enricher execution in priority order.

**Features:**
- Automatic ordering by priority
- Error isolation (one enricher failure doesn't break pipeline)
- Optional enricher skipping
- Dynamic enricher management

**Usage:**
```csharp
var pipeline = new MessageEnricherPipeline(
    new IMessageEnricher[]
    {
        new MetadataEnricher(),
        new ContextEnricher(),
        new SafetyFilterEnricher()
    },
    logger);

var context = new EnrichmentContext
{
    ConversationId = "conv123",
    UserId = "user456"
};

var enrichedMessages = await pipeline.EnrichAsync(
    originalMessages,
    context,
    cancellationToken);
```

### Built-in Enrichers

#### 1. MetadataEnricher (Priority: 10)
Adds conversation metadata to message fields.

**Configuration:**
```csharp
var enricher = new MetadataEnricher(new MetadataEnricherOptions
{
    AddConversationId = true,      // Set AgentName field
    AddFlowName = true,             // Set FlowName field
    StoreMetadataInContext = false  // Store in EnricherData
});
```

**Behavior:**
- Sets `AgentName` to conversation ID if empty
- Sets `FlowName` from context metadata
- Optionally stores message IDs and roles in context

#### 2. ContextEnricher (Priority: 20)
Manages conversation context and history.

**Configuration:**
```csharp
var enricher = new ContextEnricher(new ContextEnricherOptions
{
    AddSystemContext = true,
    AddConversationSummary = true,
    MaxMessages = 50,  // Trim to recent 50 messages
    SystemContextTemplate = "Current time: {timestamp}, User: {user_id}"
});
```

**Behavior:**
- Inserts system message with timestamp and IDs
- Adds conversation summary if available in context
- Trims old messages while preserving system messages

#### 3. SafetyFilterEnricher (Priority: 90)
Content filtering and redaction.

**Configuration:**
```csharp
var enricher = new SafetyFilterEnricher(new SafetyFilterOptions
{
    BlockedPatterns = new List<string>
    {
        @"password\s*[:=]\s*\w+",
        @"api[_-]?key\s*[:=]\s*\w+"
    },
    RedactSensitiveInfo = true,
    MaxContentLength = 10000,
    TruncationSuffix = "... [truncated]",
    BlockedContentReplacement = "[Content blocked]"
});
```

**Behavior:**
- Blocks messages matching patterns
- Redacts emails, phone numbers, credit cards
- Truncates long messages
- Marks filtered messages in Response/FunctionName fields

### Custom Enricher Example

```csharp
public class TranslationEnricher : MessageEnricherBase
{
    public override string Name => "Translation";
    public override int Priority => 50;  // Run in middle

    private readonly ITranslationService _translator;

    public override async Task<List<HazinaChatMessage>> EnrichAsync(
        List<HazinaChatMessage> messages,
        EnrichmentContext context,
        CancellationToken cancellationToken)
    {
        var enriched = CloneMessages(messages);

        // Get target language from context
        if (!context.Metadata.TryGetValue("target_language", out var lang))
            return enriched;

        var targetLang = lang.ToString();

        // Translate user messages
        foreach (var message in enriched)
        {
            if (message.Role.Role == HazinaMessageRole.User.Role)
            {
                message.Text = await _translator.TranslateAsync(
                    message.Text,
                    targetLang,
                    cancellationToken);

                message.FunctionName = $"translated_to_{targetLang}";
            }
        }

        return enriched;
    }
}
```

### Complete Pipeline Example

```csharp
// Setup pipeline with custom enricher
var pipeline = new MessageEnricherPipeline(
    new IMessageEnricher[]
    {
        new MetadataEnricher(new MetadataEnricherOptions
        {
            AddConversationId = true
        }),
        new ContextEnricher(new ContextEnricherOptions
        {
            MaxMessages = 20
        }),
        new TranslationEnricher(translationService),
        new SafetyFilterEnricher(new SafetyFilterOptions
        {
            RedactSensitiveInfo = true
        })
    },
    logger);

// Prepare context
var context = new EnrichmentContext
{
    ConversationId = "conv_123",
    UserId = "user_456",
    Metadata = new Dictionary<string, object>
    {
        ["target_language"] = "es",
        ["flow_name"] = "customer_support"
    }
};

// Enrich messages before sending to LLM
var messages = new List<HazinaChatMessage>
{
    new HazinaChatMessage(HazinaMessageRole.User, "Hello, how are you?")
};

var enrichedMessages = await pipeline.EnrichAsync(
    messages,
    context,
    cancellationToken);

// enrichedMessages now contains:
// 1. System message with timestamp and IDs (ContextEnricher)
// 2. Original message translated to Spanish (TranslationEnricher)
// 3. All sensitive info redacted (SafetyFilterEnricher)
// 4. AgentName set to conversation ID (MetadataEnricher)
```

## Architecture

### Execution Flow

```
┌─────────────────────────────────────────────────────────┐
│                    Tool Execution                        │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  1. Create ToolExecutionContext (correlation tracking)   │
│                          │                               │
│  2. Start Activity (distributed tracing)                 │
│                          │                               │
│  3. Enrich Messages (transformation pipeline)            │
│     │                                                     │
│     ├─► MetadataEnricher (priority 10)                  │
│     ├─► ContextEnricher (priority 20)                   │
│     ├─► CustomEnrichers (priority 30-80)                │
│     └─► SafetyFilterEnricher (priority 90)              │
│                          │                               │
│  4. Log Execution Start                                  │
│                          │                               │
│  5. Execute Tool                                         │
│                          │                               │
│  6. Log Success/Failure                                  │
│                          │                               │
│  7. Record Metrics                                       │
│                          │                               │
│  8. Complete Activity                                    │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

### Integration with Existing Systems

The new features integrate seamlessly with:

- **Tool Provider System (PR #244)**: Logging and enrichment work with any provider
- **LLM Clients**: Enrichers transform messages before LLM calls
- **Telemetry Systems**: Activity integration for OpenTelemetry/Azure Application Insights
- **Monitoring**: Metrics for Prometheus/Grafana dashboards

## Files Added

### Execution (Logging & Correlation)
- `src/Core/LLMs/Hazina.LLMs.Classes/Execution/ToolExecutionContext.cs` (125 lines)
- `src/Core/LLMs/Hazina.LLMs.Classes/Execution/ToolExecutionLogger.cs` (189 lines)
- `src/Core/LLMs/Hazina.LLMs.Classes/Execution/ToolExecutionMetrics.cs` (163 lines)

### Enrichment (Message Pipeline)
- `src/Core/LLMs/Hazina.LLMs.Classes/Enrichment/IMessageEnricher.cs` (53 lines)
- `src/Core/LLMs/Hazina.LLMs.Classes/Enrichment/MessageEnricherPipeline.cs` (121 lines)
- `src/Core/LLMs/Hazina.LLMs.Classes/Enrichment/BuiltIn/MetadataEnricher.cs` (60 lines)
- `src/Core/LLMs/Hazina.LLMs.Classes/Enrichment/BuiltIn/ContextEnricher.cs` (87 lines)
- `src/Core/LLMs/Hazina.LLMs.Classes/Enrichment/BuiltIn/SafetyFilterEnricher.cs` (106 lines)

**Total**: 904 lines of production code

## Dependencies

- **Microsoft.Extensions.Logging.Abstractions 9.0.0**: ILogger support
- **System.Diagnostics.Activity**: Distributed tracing
- Existing Hazina.LLMs.Classes models

## Testing

Unit tests should cover:

1. **ToolExecutionContext**: Child creation, correlation IDs, metadata
2. **ToolExecutionLogger**: All log levels, scope creation, Activity integration
3. **ToolExecutionMetrics**: Metric recording, percentile calculation, summary
4. **Enrichers**: Each enricher in isolation and in pipeline
5. **Pipeline**: Ordering, error handling, enricher skipping

## Performance Considerations

- **Logging**: Minimal overhead with structured logging
- **Enrichment**: O(n) per enricher, total O(n*m) where m = enricher count
- **Metrics**: Thread-safe, lock-based for accuracy
- **Memory**: Message cloning per enricher (consider for large conversations)

## Migration Guide

Existing code continues to work unchanged. To adopt new features:

### Add Logging

```csharp
// Before
var result = await tool.Execute(messages, toolCall, cancellationToken);

// After
var context = new ToolExecutionContext { ToolName = "my_tool" };
var toolLogger = logger.ForToolExecution();

toolLogger.LogExecutionStart(context);
var result = await tool.Execute(messages, toolCall, cancellationToken);
toolLogger.LogExecutionSuccess(context, elapsed);
```

### Add Enrichment

```csharp
// Before
var response = await llmClient.ChatAsync(messages, cancellationToken);

// After
var pipeline = new MessageEnricherPipeline(enrichers, logger);
var enrichedMessages = await pipeline.EnrichAsync(
    messages,
    new EnrichmentContext { UserId = userId },
    cancellationToken);
var response = await llmClient.ChatAsync(enrichedMessages, cancellationToken);
```

## Future Enhancements

Potential future additions:
- Async enrichers with parallel execution
- Caching layer for expensive enrichers
- Enricher composition (enricher A depends on enricher B output)
- Configuration-driven pipeline setup
- Built-in enrichers for common tasks (rate limiting, caching, retry)
- Metrics export to Prometheus
- OpenTelemetry SDK integration

## Related Tasks

- ✅ Task 869cabf4g: Structured logging + correlation (this PR)
- ✅ Task 869cabf42: Message enricher pipeline (this PR)
- ✅ Task 869cabf4d: Tool Provider pattern (PR #244)
- ✅ Task 869cabf4a: Tool validations/guardrails (PR #244)
- ✅ Task 869cabf45: Mocks/fakes for tools (PR #244)
- ✅ Task 869cabf3k: Opt-in tool sets (PR #244)

All 6 tasks from Batch 5 are now complete.
