# Hazina.AI.Memory - Agent Memory Architecture

Three-layer memory system for agents with explicit promotion policies.

## Memory Layers

### 1. WorkingMemory
Token-bounded short-term memory for active context.

```csharp
var workingMemory = new WorkingMemory(maxTokens: 8000);

// Add items
workingMemory.AddText("User wants to analyze sales data", MemoryItemType.Instruction, priority: 100);
workingMemory.AddText("Retrieved 10,000 records from database", MemoryItemType.ToolResult);

// Automatic eviction when full
Console.WriteLine($"Available tokens: {workingMemory.AvailableTokens}");

// Consolidate to text
var context = workingMemory.ConsolidateToText();
```

**Features:**
- Automatic FIFO eviction when capacity reached
- Priority-based retention
- Token counting for LLM context management
- Type-based filtering

### 2. EpisodicMemoryStore
Timestamped event log for temporal reasoning.

```csharp
var episodicMemory = new EpisodicMemoryStore(maxEpisodes: 1000);

// Record events
episodicMemory.RecordToolExecution(
    "DatabaseQuery",
    new Dictionary<string, object> { ["query"] = "SELECT * FROM sales" },
    result: "10000 rows",
    success: true
);

episodicMemory.RecordDecision(
    "Use median aggregation",
    "Data contains outliers that would skew mean calculation"
);

episodicMemory.RecordTaskSummary(
    "Sales Analysis",
    "Analyzed Q1-Q4 sales, identified 23% growth in Q3",
    success: true,
    duration: TimeSpan.FromMinutes(5)
);

// Query by time
var recentEpisodes = episodicMemory.GetRecentEpisodes(10);
var todayEpisodes = episodicMemory.GetEpisodesInRange(
    DateTime.Today,
    DateTime.UtcNow
);

// Statistics
var stats = episodicMemory.GetStatistics();
```

**Features:**
- Automatic rotation when max capacity reached
- Time-range queries
- Type filtering
- Search by description

### 3. SemanticMemoryStore
Long-term embedding-based knowledge.

```csharp
// Use existing embedding infrastructure
var semanticMemory = new SemanticMemoryStoreAdapter(embeddingStore, vectorSearchStore);

// Store knowledge
await semanticMemory.StoreAsync(
    key: "sales-insight-2024-q3",
    text: "Q3 2024 showed 23% YoY growth driven by new product launches",
    embedding: embedding,
    metadata: new Dictionary<string, object> { ["quarter"] = "Q3", ["year"] = 2024 }
);

// Search by similarity
var queryEmbedding = await GenerateEmbedding("What drove growth in 2024?");
var results = await semanticMemory.SearchAsync(queryEmbedding, topK: 5);
```

**Features:**
- Backed by existing vector search infrastructure
- No behavior change to underlying stores
- Consistent interface across memory types

## Memory Policy

Decides what gets promoted between layers.

```csharp
var policy = new MemoryPolicy(new MemoryPolicyOptions
{
    PromoteToolResultsToEpisodic = true,
    PromoteTaskSummariesToSemantic = true,
    WorkingMemoryConsolidationThreshold = 0.9
});

// Check promotion
var item = new MemoryItem { /* ... */ };
if (policy.ShouldPromoteToEpisodic(item))
{
    // Promote to episodic memory
}
```

**Promotion Rules:**
- **Working → Episodic**: Decisions, high-priority items, tool results
- **Episodic → Semantic**: Task summaries, successes, optionally errors
- **Working consolidation**: When utilization > threshold

## Memory Manager

Orchestrates all layers with automatic promotion.

```csharp
var manager = new MemoryManager(
    workingMemory,
    episodicMemory,
    semanticMemory,
    policy
);

// Single call applies all policies
var item = new MemoryItem
{
    ItemId = Guid.NewGuid().ToString(),
    Content = "Calculated median: $45,230",
    Type = MemoryItemType.Decision,
    TokenCount = 15,
    Priority = 80
};

manager.Remember(item); // Auto-promotes to episodic if policy allows

// Auto-consolidate
manager.ConsolidateIfNeeded(); // Checks threshold and consolidates if needed
```

## Design Principles

1. **Explicit separation** - Working, episodic, semantic have distinct purposes
2. **Configurable promotion** - Policies control what moves between layers
3. **Token-aware** - Working memory respects LLM context limits
4. **No hidden state** - All memory operations are explicit
5. **Backward compatible** - Semantic layer wraps existing infrastructure

## Use Cases

### Short-lived agents
Use WorkingMemory only for context management.

### Interactive agents
WorkingMemory + EpisodicMemory for conversation history and learning.

### Long-running agents
Full stack (Working + Episodic + Semantic) for knowledge accumulation.

### Multi-session agents
Serialize EpisodicMemory between sessions for continuity.

## Integration Example

```csharp
public class MemoryAwareAgent
{
    private readonly MemoryManager _memory;

    public async Task ExecuteTask(string task)
    {
        // Add task to working memory
        _memory.WorkingMemory.AddText(task, MemoryItemType.Instruction, priority: 100);

        // Do work...
        var result = await SomeToolCall();

        // Remember result
        _memory.Remember(new MemoryItem
        {
            ItemId = Guid.NewGuid().ToString(),
            Content = result,
            Type = MemoryItemType.ToolResult,
            TokenCount = EstimateTokens(result)
        });

        // Consolidate if needed
        _memory.ConsolidateIfNeeded();

        // Use working memory as context for next LLM call
        var context = _memory.WorkingMemory.ConsolidateToText();
    }
}
```

## Memory Persistence

```csharp
// Save episodic memory to disk
var episodes = JsonSerializer.Serialize(episodicMemory.Episodes);
await File.WriteAllTextAsync("episodes.json", episodes);

// Load on next run
var loadedEpisodes = JsonSerializer.Deserialize<List<Episode>>(json);
foreach (var episode in loadedEpisodes)
{
    episodicMemory.AddEpisode(episode);
}
```

Semantic memory is automatically persisted via the underlying embedding store.
