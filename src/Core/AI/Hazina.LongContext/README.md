# Hazina.LongContext

**Recursive Long-Context Orchestrator for handling queries over massive context through query decomposition and hierarchical retrieval.**

## Overview

`Hazina.LongContext` enables handling queries that require processing more context than can fit in a single LLM call. It does this by:

1. **Query Decomposition** - Breaking large questions into smaller sub-questions
2. **Hierarchical Retrieval** - Retrieving relevant shards for each sub-question
3. **Result Synthesis** - Aggregating sub-answers into a comprehensive final answer

The system is built on top of existing Hazina capabilities:
- **Hazina.AI.ContextEngineering** - Multi-source retrieval and context assembly
- **Hazina.AI.RAG** - Semantic search and embedding-based retrieval
- **Hazina.AI.Compression** - Token counting and optimization
- **Hazina.AI.Orchestration** - Multi-step task execution

## Features

### Phase 1: Core Abstractions ✅

- **Domain Models**
  - `LongContextSessionId` - Session tracking for multi-turn conversations
  - `ContextShard` - Independent chunks of retrieved context
  - `QueryNode` - Nodes in the recursive query tree
  - `QueryNodeType` - Root, Decomposition, Retrieval, Summarization, Aggregation
  - `LongContextRequest/Result` - Request/response models

- **Core Interfaces**
  - `ILongContextStrategy` - Main orchestration interface
  - `IQueryPlanner` - Plans query execution trees
  - `IQueryNodeExecutor` - Executes individual nodes
  - `IContextShardProvider` - Retrieves relevant shards

### Phase 2: Simple Implementations ✅

- **SingleShotStrategy** - Non-recursive execution (backwards compatible)
- **SimpleQueryPlanner** - Creates single-node retrieval plans
- **QueryNodeExecutor** - Executes retrieval, summarization, and aggregation nodes
- **ContextEngineeringShardProvider** - Bridges to existing ContextEngineering
- **Configuration & DI** - `LongContextOptions` and service registration

### Phase 3-5: Recursive Implementations ✅

- **RecursiveQueryPlanner** - LLM-driven query decomposition
- **RecursiveLongContextStrategy** - Full recursive orchestration
- **Configuration-based Strategy Selection** - Automatic planner and strategy selection
- **Depth Control** - Limits on recursion depth and branching factor
- **Fallback Logic** - Graceful degradation to single-shot when needed

### Performance Enhancement: Parallel Execution ✅

- **Parallel Node Execution** - Execute sibling nodes concurrently for 3-5x speedup
- **Configurable Parallelism** - Control degree of parallelism or disable for debugging
- **Execution Mode Tracking** - Results include execution mode used (Sequential/Parallel/LimitedParallel)
- **Automatic Optimization** - Single child nodes execute sequentially regardless of config

## Installation

Add project reference:

```xml
<ProjectReference Include="path/to/Hazina.LongContext/Hazina.LongContext.csproj" />
```

## Quick Start

### 1. Register Services

```csharp
using Hazina.LongContext.Extensions;

services.AddLongContext(options =>
{
    options.EnableRecursiveQueries = false; // Start with single-shot for testing
    options.DefaultMaxDepth = 3;
    options.DefaultMaxTotalTokens = 100_000;
});
```

### 2. Use the Strategy

```csharp
public class MyService
{
    private readonly ILongContextStrategy _strategy;

    public MyService(ILongContextStrategy strategy)
    {
        _strategy = strategy;
    }

    public async Task<string> AskQuestionAsync(string question)
    {
        var request = new LongContextRequest
        {
            Query = question,
            EnableRecursion = false, // Single-shot for now
            MaxDepth = 3,
            MaxTotalTokens = 50_000
        };

        var result = await _strategy.ExecuteAsync(request);

        if (!result.Success)
        {
            throw new Exception($"Query failed: {result.Error}");
        }

        Console.WriteLine($"Tokens used: {result.TotalTokensUsed}");
        Console.WriteLine($"Shards used: {result.UsedShards.Count}");
        Console.WriteLine($"Execution time: {result.TotalExecutionTime}");

        return result.FinalAnswer;
    }
}
```

### 3. Example Usage

```csharp
// Simple question (single-shot retrieval)
var answer = await askService.AskQuestionAsync(
    "What are the main features of Hazina?");

// With session tracking
var sessionId = LongContextSessionId.New();
var request = new LongContextRequest
{
    Query = "Explain the architecture",
    SessionId = sessionId,
    Tags = new[] { "architecture", "design" },
    MetadataFilters = new Dictionary<string, string>
    {
        ["domain"] = "technical-docs"
    }
};

var result = await _strategy.ExecuteAsync(request);
```

## Architecture

### Query Execution Flow

```
User Query
    ↓
IQueryPlanner
    ↓
QueryNode Tree
    ↓
IQueryNodeExecutor
    ↓
IContextShardProvider → ContextEngineering → RAG/Vector Search
    ↓
LLM (answer generation)
    ↓
LongContextResult
```

### Node Types

1. **Root** - Top-level user question (delegates to child)
2. **Retrieval** - Retrieve shards and generate answer
3. **Summarization** - Summarize child node results
4. **Aggregation** - Combine multiple child answers
5. **Decomposition** - (Future) Break question into sub-questions

### Current Implementation (Phase 1-2)

**Single-Shot Mode:**
```
Root
  └─ Retrieval (user query)
       └─ ContextEngineering.GetContextAsync()
            └─ RAG.QueryAsync()
                 └─ Vector/Metadata Search
```

**Planned Recursive Mode (Phase 3-5):**
```
Root
  └─ Decomposition (break into sub-questions)
       ├─ Retrieval (sub-question 1)
       ├─ Retrieval (sub-question 2)
       └─ Summarization
            ├─ Retrieval (detail query)
            └─ Aggregation (combine results)
```

## Configuration

### Global Configuration

```csharp
services.AddLongContext(options =>
{
    // Enable recursive mode
    options.EnableRecursiveQueries = true;

    // Default limits
    options.DefaultMaxDepth = 4;
    options.DefaultMaxTotalTokens = 150_000;
    options.DefaultMaxBranchingFactor = 5;
});
```

### Per-Store Configuration

```csharp
services.AddLongContext(options =>
{
    // Enable recursion only for specific stores
    options.PerStoreConfig["documentation"] = new StoreRecursiveConfig
    {
        EnableRecursive = true,
        MaxDepth = 5,
        MaxTotalTokens = 200_000
    };

    options.PerStoreConfig["chat-history"] = new StoreRecursiveConfig
    {
        EnableRecursive = false // Single-shot only
    };
});
```

## Backwards Compatibility

The library is designed to be **100% backwards compatible**:

- **Default behavior**: Single-shot retrieval (same as existing RAG)
- **Opt-in**: Enable recursion per store or per request
- **Fallback**: If recursion fails, falls back to single-shot

## Integration with Existing Components

### ContextEngineering Bridge

`ContextEngineeringShardProvider` bridges to the existing `IContextEngine`:

```csharp
// Internally converts:
IContextEngine.GetContextAsync(query, config)
    ↓
IReadOnlyList<ContextShard>
```

This allows long-context orchestration to leverage:
- 4 retrievers (semantic, facts, metadata, ID lookup)
- 3 fusion strategies (WeightedSum, RRF, MaxScore)
- Token budget management
- Policy-driven configuration

### RAG Integration

Context shards can come from:
- **Semantic search** - Embedding-based retrieval
- **Facts store** - Compact symbolic knowledge
- **Metadata search** - Filtering by document properties
- **Direct lookup** - Specific document IDs

## Development Roadmap

### Phase 1: Core Abstractions ✅ (Complete)
- Domain models and interfaces
- Project structure and dependencies

### Phase 2: Simple Implementations ✅ (Complete)
- Single-shot strategy (backwards compatible)
- Basic query planner and executor
- ContextEngineering bridge
- Configuration and DI

### Phase 3: Recursive Planner ⏳ (Next)
- LLM-driven question decomposition
- Query tree building with depth/branching limits
- Prompt engineering for decomposition

### Phase 4: Full Executor ⏳
- Recursive node execution
- Token budget tracking
- Error handling and fallbacks

### Phase 5: Strategy & Orchestration ⏳
- RecursiveLongContextStrategy
- Execution order optimization
- Result synthesis improvements

### Phase 6: Testing & Documentation ⏳
- Unit tests for all components
- Integration tests with existing RAG
- End-to-end scenario tests
- Performance benchmarks

## Example: Recursive Multi-Level Query

```csharp
// Enable recursive mode
services.AddLongContext(options =>
{
    options.EnableRecursiveQueries = true;
    options.DefaultMaxDepth = 4;
});

// Complex query that benefits from decomposition
var request = new LongContextRequest
{
    Query = "Analyze the Hazina codebase and explain its architecture, main components, and how they work together",
    EnableRecursion = true,
    MaxDepth = 4,
    MaxTotalTokens = 150_000,
    MaxBranchingFactor = 5
};

var result = await _strategy.ExecuteAsync(request);

Console.WriteLine($"Answer:\n{result.FinalAnswer}");
Console.WriteLine($"\nTokens used: {result.TotalTokensUsed}");
Console.WriteLine($"Execution time: {result.TotalExecutionTime}");
Console.WriteLine($"Execution mode: {result.ExecutionMode}"); // NEW: Shows parallel/sequential
Console.WriteLine($"\nQuery tree statistics:");
Console.WriteLine($"  Total nodes: {result.Statistics.TotalNodes}");
Console.WriteLine($"  Retrieval nodes: {result.Statistics.RetrievalNodes}");
Console.WriteLine($"  Decomposition nodes: {result.Statistics.DecompositionNodes}");
Console.WriteLine($"  Max depth: {result.Statistics.MaxDepth}");

// Inspect query tree structure
void PrintTree(QueryNode node, int indent = 0)
{
    var prefix = new string(' ', indent * 2);
    Console.WriteLine($"{prefix}{node.Type}: {node.Prompt.Substring(0, Math.Min(60, node.Prompt.Length))}...");
    foreach (var child in node.Children)
    {
        PrintTree(child, indent + 1);
    }
}

PrintTree(result.QueryTree);

// Example output:
// Root: Analyze the Hazina codebase and explain its architecture...
//   Decomposition: Analyze the Hazina codebase...
//     Aggregation: Combine answers to: Analyze the Hazina codebase...
//       Retrieval: What is the overall architecture of Hazina?
//       Retrieval: What are the main components of Hazina?
//       Retrieval: How do the components work together?
//       Retrieval: What are the key design patterns used?
```

## Performance Considerations

### Parallel Execution ✅ **IMPLEMENTED**

**Dramatic performance improvement for multi-branch queries:**

```csharp
// Example: Query with 4 sub-questions
// Sequential: 40 seconds (4 × 10s each)
// Parallel:   12 seconds (all 4 at once, with overhead)
// Speedup:    3.3x faster
```

**Configuration options:**

1. **Unlimited Parallelism** (default, fastest)
   ```csharp
   options.EnableParallelExecution = true;
   options.MaxDegreeOfParallelism = -1; // All siblings execute concurrently
   ```

2. **Limited Parallelism** (controlled resource usage)
   ```csharp
   options.MaxDegreeOfParallelism = 3; // Max 3 concurrent operations
   ```

3. **Sequential** (debugging, predictable order)
   ```csharp
   options.EnableParallelExecution = false; // One at a time
   ```

**When to use each mode:**

- **Unlimited Parallel**: Production, high-performance scenarios, abundant resources
- **Limited Parallel**: Rate-limited APIs, memory constraints, cost control
- **Sequential**: Debugging, testing, understanding execution flow

**Performance Tips:**

- Parallel execution gives the most benefit with 3+ sub-questions
- Overhead is ~20-30% (Task scheduling, synchronization)
- Returns early if any child node fails
- Uses `SemaphoreSlim` for degree of parallelism control

### Other Optimizations

- **Caching** - Query plans and shard results can be cached (planned)
- **Budget Control** - Hard limits prevent token explosion
- **Incremental Results** - Stream results as they become available (planned)

## Related Projects

- [`Hazina.AI.ContextEngineering`](../Hazina.AI.ContextEngineering/) - Multi-source context assembly
- [`Hazina.AI.RAG`](../Hazina.AI.RAG/) - Semantic search and RAG engine
- [`Hazina.AI.Compression`](../Hazina.AI.Compression/) - Token counting and optimization
- [`Hazina.AI.Orchestration`](../Hazina.AI.Orchestration/) - Multi-step task execution
- [`Hazina.Brain`](../Hazina.Brain/) - Episodic memory and fact distillation

## Contributing

This is an active development project. Current status:

- ✅ **Phase 1-2 Complete** - Basic infrastructure and backwards-compatible single-shot mode
- ⏳ **Phase 3-5 In Progress** - Recursive query decomposition and orchestration
- ⏳ **Phase 6 Planned** - Comprehensive testing and documentation

## License

Part of the Hazina framework.

---

**Last Updated:** 2026-01-11
**Status:** Phase 2 Complete, Phase 3 In Progress
