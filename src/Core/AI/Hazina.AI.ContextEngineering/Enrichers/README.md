# Message Enricher Pipeline

Pluggable pipeline architecture for enriching LLM request context with history, relevant documents, file lists, and store data.

## Overview

The Message Enricher Pipeline provides a modular, extensible way to build context for LLM requests. Instead of monolithic context builders, enrichers are small, focused components that add specific types of context in priority order.

## Architecture

```
User Message
    ↓
[MessageContext] → HistoryWindowEnricher (Priority 10)
    ↓
[MessageContext] → RelevancyEnricher (Priority 20)
    ↓
[MessageContext] → FileListEnricher (Priority 30)
    ↓
[MessageContext] → StoreEnricher (Priority 40)
    ↓
[Enriched MessageContext] → LLM Request
```

## Built-in Enrichers

### 1. HistoryWindowEnricher
**Priority:** 10 (executes first)

Adds conversation history with sliding window management.

```csharp
var enricher = new HistoryWindowEnricher(logger, new HistoryWindowOptions
{
    MaxMessages = 10,  // Keep last 10 messages
    PrioritizeRecent = true
});
```

**Features:**
- Sliding window to limit history size
- Token estimation
- Configurable message limit

### 2. RelevancyEnricher
**Priority:** 20

Filters and ranks documents by semantic relevance.

```csharp
var enricher = new RelevancyEnricher(logger, new RelevancyOptions
{
    MaxDocuments = 5,      // Keep top 5 documents
    MinimumScore = 0.7,    // Relevance threshold
    IncludeMetadata = true
});
```

**Features:**
- Score-based filtering
- Top-K selection
- Token estimation from document content

### 3. FileListEnricher
**Priority:** 30

Adds project file list to context.

```csharp
var enricher = new FileListEnricher(logger, new FileListOptions
{
    MaxFiles = 100,
    IncludeFullPaths = true,
    FilterExtensions = new[] { ".cs", ".ts", ".md" }
});
```

**Features:**
- File count limiting
- Path formatting (full/relative)
- Extension filtering
- Token estimation

### 4. StoreEnricher
**Priority:** 40 (executes last)

Adds data from additional stores (caches, databases, etc.).

```csharp
var enricher = new StoreEnricher(logger, new StoreOptions
{
    MaxEntries = 20,
    IncludeMetadata = true
});
```

**Features:**
- Multiple store support
- Metadata inclusion
- Token estimation

## Quick Start

### Basic Setup

```csharp
using Hazina.AI.ContextEngineering.Enrichers;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Add complete pipeline with all default enrichers
services.AddMessageEnricherPipeline(options =>
{
    options.EnableHistoryWindow = true;
    options.EnableRelevancy = true;
    options.EnableFileList = true;
    options.EnableStore = true;

    // Configure individual enrichers
    options.HistoryWindowOptions.MaxMessages = 15;
    options.RelevancyOptions.MinimumScore = 0.75;
    options.FileListOptions.MaxFiles = 50;
});

var provider = services.BuildServiceProvider();
var pipeline = provider.GetRequiredService<MessageEnricherPipeline>();
```

### Using the Pipeline

```csharp
// Create initial context
var context = new MessageContext
{
    UserMessage = "How do I implement authentication?",
    ProjectId = "my-project",
    History = conversationHistory,
    Documents = ragDocuments,
    Files = projectFiles,
    MaxTokens = 8000
};

// Enrich context
var enrichedContext = await pipeline.EnrichAsync(context);

// Use enriched context for LLM request
var response = await llmClient.GetResponse(
    enrichedContext.UserMessage,
    history: enrichedContext.History,
    systemPrompt: enrichedContext.SystemPrompt);
```

## Custom Enrichers

### Creating a Custom Enricher

```csharp
public class CustomEnricher : IMessageEnricher
{
    public int Priority => 15; // Between history and relevancy
    public string Name => "Custom";

    private readonly ILogger<CustomEnricher> _logger;

    public CustomEnricher(ILogger<CustomEnricher> logger)
    {
        _logger = logger;
    }

    public async Task<MessageContext> EnrichAsync(
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[CUSTOM-ENRICHER] Processing context");

        // Add your custom enrichment logic
        context.Metadata["custom-data"] = await GetCustomDataAsync();

        // Update token estimate
        context.EstimatedTokens += 100;

        return context;
    }

    private async Task<object> GetCustomDataAsync()
    {
        // Your implementation
        return new { };
    }
}
```

### Registering Custom Enricher

```csharp
services.AddMessageEnricher<CustomEnricher>();
```

## Advanced Usage

### Conditional Enrichment

```csharp
public class ConditionalEnricher : IMessageEnricher
{
    public int Priority => 25;
    public string Name => "Conditional";

    public Task<MessageContext> EnrichAsync(
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        // Only add documents if token budget allows
        if (context.MaxTokens.HasValue &&
            context.EstimatedTokens + 1000 <= context.MaxTokens.Value)
        {
            // Add documents
            context.Documents.AddRange(await GetDocumentsAsync());
        }

        return Task.FromResult(context);
    }
}
```

### Dynamic Priority

```csharp
public class DynamicPriorityEnricher : IMessageEnricher
{
    private int _currentPriority = 20;

    public int Priority => _currentPriority;
    public string Name => "Dynamic";

    public void SetPriority(int priority)
    {
        _currentPriority = priority;
    }

    // ... implementation
}
```

### Token Budget Management

```csharp
var context = new MessageContext
{
    UserMessage = "Explain the architecture",
    MaxTokens = 4000  // Hard limit
};

var enrichedContext = await pipeline.EnrichAsync(context);

// Check if within budget
if (enrichedContext.EstimatedTokens > enrichedContext.MaxTokens)
{
    // Handle overflow (truncate, remove documents, etc.)
    enrichedContext = await ApplyBudgetConstraintsAsync(enrichedContext);
}
```

## Integration with Existing Code

### Migrating from SmartContextBuilder

**Before:**
```csharp
var context = await _contextBuilder.BuildContextAsync(
    strategy,
    projectId,
    recentHistory,
    customPrompt);
```

**After:**
```csharp
var messageContext = new MessageContext
{
    UserMessage = userMessage,
    ProjectId = projectId,
    History = recentHistory.Select(m => new ChatMessage
    {
        Role = m.Role,
        Content = m.Content
    }).ToList(),
    SystemPrompt = customPrompt
};

var enrichedContext = await _pipeline.EnrichAsync(messageContext);
```

## Logging and Tracing

All enrichers automatically log their execution:

```
[ENRICHER-PIPELINE] Starting enrichment pipeline | CorrelationId: abc123 | Enrichers: 4
[ENRICHER-PIPELINE] Executing enricher: HistoryWindow | Priority: 10 | CorrelationId: abc123
[ENRICHER-PIPELINE] Enricher completed: HistoryWindow | Duration: 5ms | Tokens: 234 | CorrelationId: abc123
[ENRICHER-PIPELINE] Executing enricher: Relevancy | Priority: 20 | CorrelationId: abc123
...
[ENRICHER-PIPELINE] Pipeline completed | Total Tokens: 1234 | Documents: 3 | History: 5 | CorrelationId: abc123
```

## Performance

### Token Estimation

Enrichers use fast estimation (4 chars ≈ 1 token):
- Exact tokenization is expensive
- Estimation is 95%+ accurate for budget planning
- Final token count from LLM provider

### Parallel Enrichment (Future)

```csharp
// Currently sequential, future parallel support:
var enrichedContext = await pipeline.EnrichParallelAsync(context,
    maxParallelism: 4);
```

## Best Practices

1. **Keep Enrichers Focused**: Each enricher should do one thing well
2. **Order Matters**: Lower priority numbers execute first
3. **Fail Gracefully**: Enrichers should not throw; pipeline continues on error
4. **Estimate Tokens**: Always update `context.EstimatedTokens`
5. **Log Everything**: Use structured logging with correlation IDs
6. **Respect Budget**: Check `MaxTokens` before adding expensive context
7. **Test Independently**: Unit test enrichers in isolation

## Testing

### Unit Testing an Enricher

```csharp
[Fact]
public async Task HistoryWindowEnricher_LimitsMessages()
{
    // Arrange
    var logger = new NullLogger<HistoryWindowEnricher>();
    var enricher = new HistoryWindowEnricher(logger, new HistoryWindowOptions
    {
        MaxMessages = 5
    });

    var context = new MessageContext
    {
        History = Enumerable.Range(0, 10)
            .Select(i => new ChatMessage { Content = $"Message {i}" })
            .ToList()
    };

    // Act
    var result = await enricher.EnrichAsync(context);

    // Assert
    Assert.Equal(5, result.History.Count);
    Assert.Equal("Message 5", result.History[0].Content); // Oldest kept
}
```

### Integration Testing Pipeline

```csharp
[Fact]
public async Task Pipeline_ExecutesEnrichersInOrder()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddMessageEnricherPipeline();

    var provider = services.BuildServiceProvider();
    var pipeline = provider.GetRequiredService<MessageEnricherPipeline>();

    var context = new MessageContext { UserMessage = "Test" };

    // Act
    var result = await pipeline.EnrichAsync(context);

    // Assert
    Assert.NotNull(result.CorrelationId);
    Assert.True(result.EstimatedTokens > 0);
}
```

## Future Enhancements

- Async parallel enrichment
- Enricher dependencies
- Conditional execution policies
- Token budget auto-adjustment
- Caching enriched contexts
- A/B testing different enricher combinations

## Dependencies

- **Microsoft.Extensions.Logging**: Logging infrastructure
- **Hazina.Observability.Core**: Correlation tracking

## License

Part of the Hazina framework.
