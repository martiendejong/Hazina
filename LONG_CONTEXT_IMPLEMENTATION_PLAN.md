# Long-Context Orchestrator Implementation Plan

**Date**: 2026-01-11
**Author**: Claude Sonnet 4.5
**Status**: Planning Phase

## Executive Summary

Implement a **Recursive Long-Context Orchestrator** on top of Hazina's existing RAG and Context Engineering capabilities. This will enable handling queries over massive context by decomposing them into hierarchical sub-queries, retrieving relevant shards, and synthesizing results.

## Current State Analysis

### What Hazina Already Has ✅

1. **Hazina.AI.RAG** - Single-shot RAG engine with:
   - Embedding-based and metadata-based retrieval
   - Composite scoring (tag relevance, recency, position)
   - Vector store integration
   - Query → Retrieve → Generate pipeline

2. **Hazina.AI.ContextEngineering** - Multi-source context assembly with:
   - 4 retrievers (semantic, facts, metadata, ID lookup)
   - 3 fusion strategies (WeightedSum, RRF, MaxScore)
   - Token budget management and packing
   - Policy-driven configuration (7 built-in presets)
   - Section-based context formatting

3. **Hazina.AI.Orchestration** - Task execution framework:
   - Multi-step task definitions
   - Progress reporting
   - Task cancellation

4. **Hazina.AI.Compression** - Token counting and optimization
5. **Hazina.AI.Memory** - Episodic memory (for storing learnings)
6. **Hazina.Brain** - Empty project (placeholder for orchestration)

### What's Missing ❌

1. **Recursive Query Planning** - Breaking queries into hierarchical sub-queries
2. **Query Tree Structure** - Representing decomposed queries as a tree
3. **Shard Abstraction** - Treating context chunks as independent shards
4. **Sub-Question Generation** - LLM-driven question decomposition
5. **Result Aggregation** - Combining sub-query answers into final answer
6. **Depth & Budget Control** - Limiting recursion and token usage
7. **Long-Context Session** - Multi-turn recursive query tracking

## Proposed Architecture

### New Library: `Hazina.LongContext`

Located at: `src/Core/AI/Hazina.LongContext/`

**Purpose**: Orchestrate recursive queries over large context by decomposing queries, delegating to existing retrieval layers, and synthesizing results.

### Core Components

#### 1. Domain Models (`Models/`)

```csharp
// Session tracking
public record LongContextSessionId(Guid Value);

// Context shard (wrapper around retrieved chunks)
public record ContextShardId(string Value);
public class ContextShard
{
    public ContextShardId Id { get; init; }
    public string Content { get; init; }
    public Dictionary<string, object> Metadata { get; init; }
    public double Relevance { get; init; }
}

// Query tree structure
public enum QueryNodeType
{
    Root,              // Top-level user question
    Decomposition,     // Question broken into sub-questions
    Retrieval,         // Retrieve shards for a question
    Summarization,     // Summarize retrieved content
    Aggregation        // Combine sub-answers
}

public class QueryNode
{
    public Guid Id { get; init; }
    public QueryNodeType Type { get; init; }
    public string Prompt { get; init; }
    public IReadOnlyList<ContextShardId> TargetShards { get; init; }
    public IReadOnlyList<QueryNode> Children { get; init; }
    public QueryNodeStatus Status { get; init; }
}

public enum QueryNodeStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

// Execution results
public class QueryNodeResult
{
    public Guid NodeId { get; init; }
    public string Answer { get; init; }
    public IReadOnlyList<ContextShard> UsedShards { get; init; }
    public int TokensUsed { get; init; }
    public TimeSpan ExecutionTime { get; init; }
}

// Request/Response
public class LongContextRequest
{
    public string Query { get; init; }
    public LongContextSessionId? SessionId { get; init; }
    public int MaxDepth { get; init; } = 3;
    public int MaxTotalTokens { get; init; } = 100_000;
    public int MaxBranchingFactor { get; init; } = 5;
    public string? StoreId { get; init; }  // Optional: target specific store
}

public class LongContextResult
{
    public string FinalAnswer { get; init; }
    public QueryNode QueryTree { get; init; }
    public IReadOnlyList<ContextShard> UsedShards { get; init; }
    public int TotalTokensUsed { get; init; }
    public TimeSpan TotalExecutionTime { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}
```

#### 2. Core Interfaces (`Interfaces/`)

```csharp
// Main orchestration interface
public interface ILongContextStrategy
{
    Task<LongContextResult> ExecuteAsync(
        LongContextRequest request,
        CancellationToken ct = default);
}

// Query planning
public interface IQueryPlanner
{
    Task<QueryNode> PlanAsync(
        LongContextRequest request,
        CancellationToken ct = default);
}

// Node execution
public interface IQueryNodeExecutor
{
    Task<QueryNodeResult> ExecuteNodeAsync(
        QueryNode node,
        CancellationToken ct = default);
}

// Shard provider (bridges to existing retrieval)
public interface IContextShardProvider
{
    Task<IReadOnlyList<ContextShard>> FindRelevantShardsAsync(
        string query,
        LongContextSessionId? sessionId,
        int maxShards = 20,
        CancellationToken ct = default);
}
```

#### 3. Implementations

**Query Planner** (`Planning/`)
- `SimpleQueryPlanner` - Single-node plan (backwards compatibility)
- `RecursiveQueryPlanner` - Decomposes queries using LLM
  - Calls LLM: "Break this question into sub-questions"
  - Creates tree of QueryNodes
  - Respects MaxDepth and MaxBranchingFactor

**Node Executor** (`Execution/`)
- `QueryNodeExecutor` - Executes different node types:
  - **Retrieval nodes**: Calls `IContextShardProvider`
  - **Summarization nodes**: Aggregates child results, calls LLM
  - **Decomposition nodes**: Executes children, combines results

**Shard Provider** (`Providers/`)
- `ContextEngineeringShardProvider` - Bridges to existing `IContextEngine`:
  - Maps `FindRelevantShardsAsync` → `GetContextAsync`
  - Converts `RetrievalResult` → `ContextShard`

**Strategy** (`Strategies/`)
- `SingleShotStrategy` - Non-recursive (adapter for backwards compatibility)
- `RecursiveLongContextStrategy` - Full recursive orchestration:
  1. Plan query tree
  2. Execute tree (depth-first or breadth-first)
  3. Aggregate results
  4. Return final answer

#### 4. Configuration (`Configuration/`)

```csharp
public class LongContextOptions
{
    public bool EnableRecursiveQueries { get; set; } = false;
    public int DefaultMaxDepth { get; set; } = 3;
    public int DefaultMaxTotalTokens { get; set; } = 100_000;
    public int DefaultMaxBranchingFactor { get; set; } = 5;

    // Per-store configuration
    public Dictionary<string, StoreRecursiveConfig> PerStoreConfig { get; set; } = new();
}

public class StoreRecursiveConfig
{
    public bool EnableRecursive { get; set; }
    public int? MaxDepth { get; set; }
    public int? MaxTotalTokens { get; set; }
}
```

#### 5. DI Registration (`Extensions/`)

```csharp
public static class LongContextServiceExtensions
{
    public static IServiceCollection AddLongContext(
        this IServiceCollection services,
        Action<LongContextOptions>? configure = null)
    {
        // Register options
        if (configure != null)
            services.Configure(configure);

        // Register core services
        services.AddSingleton<IQueryPlanner, RecursiveQueryPlanner>();
        services.AddSingleton<IQueryNodeExecutor, QueryNodeExecutor>();
        services.AddSingleton<IContextShardProvider, ContextEngineeringShardProvider>();

        // Register strategies
        services.AddSingleton<ILongContextStrategy, RecursiveLongContextStrategy>();

        return services;
    }
}
```

## Integration Strategy

### Backwards Compatibility

1. **Default behavior unchanged**: If `EnableRecursiveQueries = false`, uses `SingleShotStrategy`
2. **Opt-in per store**: Enable recursion only for specific stores
3. **Fallback**: If recursion disabled or fails, falls back to existing RAG pipeline

### Integration Points

1. **Retrieval Layer**: `IContextShardProvider` wraps `IContextEngine` (from ContextEngineering)
2. **LLM Calls**: Uses existing `IProviderOrchestrator` for all LLM interactions
3. **Token Counting**: Uses existing `Hazina.AI.Compression` utilities
4. **Memory**: Can optionally store query trees in `Hazina.Brain` for learning

## Implementation Phases

### Phase 1: Core Abstractions (2 hours)
- ✅ Create `Hazina.LongContext` project
- ✅ Define domain models (`QueryNode`, `ContextShard`, etc.)
- ✅ Define core interfaces
- ✅ Add project references to existing Hazina components

### Phase 2: Simple Implementations (3 hours)
- ✅ `SimpleQueryPlanner` - Single-node plan
- ✅ `SingleShotStrategy` - Non-recursive adapter
- ✅ `ContextEngineeringShardProvider` - Bridge to existing retrieval
- ✅ Basic `QueryNodeExecutor` - Execute retrieval nodes only

### Phase 3: Recursive Planner (4 hours)
- ✅ `RecursiveQueryPlanner` - LLM-driven decomposition
- ✅ Prompt engineering for question breaking
- ✅ Tree building logic
- ✅ Depth and branching factor limits

### Phase 4: Full Executor (4 hours)
- ✅ Extend `QueryNodeExecutor` for all node types
- ✅ Summarization nodes (call LLM to summarize children)
- ✅ Aggregation nodes (combine child answers)
- ✅ Error handling and fallbacks

### Phase 5: Strategy & Orchestration (3 hours)
- ✅ `RecursiveLongContextStrategy` - Full recursive flow
- ✅ Token budget tracking across recursive calls
- ✅ Execution order (depth-first vs breadth-first)
- ✅ Result synthesis

### Phase 6: Configuration & DI (2 hours)
- ✅ `LongContextOptions` with validation
- ✅ Service registration extensions
- ✅ Per-store configuration
- ✅ Strategy selector (choose based on config)

### Phase 7: Documentation & Examples (2 hours)
- ✅ Comprehensive README.md
- ✅ Code examples
- ✅ Integration guide
- ✅ Configuration examples

### Phase 8: Testing (4 hours)
- ✅ Unit tests for core components
- ✅ Integration tests with existing RAG
- ✅ End-to-end scenario tests
- ✅ Performance benchmarks

**Total Estimated Time**: 24 hours (3 working days)

## Example Usage

### Basic (Single-Shot, Backwards Compatible)

```csharp
// Default: uses existing RAG pipeline
var result = await longContextStrategy.ExecuteAsync(new LongContextRequest
{
    Query = "What are the main features of Hazina?"
});
Console.WriteLine(result.FinalAnswer);
```

### Recursive (New Capability)

```csharp
// Enable recursion
services.AddLongContext(options =>
{
    options.EnableRecursiveQueries = true;
    options.DefaultMaxDepth = 4;
    options.DefaultMaxTotalTokens = 150_000;
});

// Execute recursive query
var result = await longContextStrategy.ExecuteAsync(new LongContextRequest
{
    Query = "Analyze all documentation in the codebase and create a comprehensive architecture guide",
    MaxDepth = 5,
    MaxTotalTokens = 200_000
});

// Inspect query tree
PrintQueryTree(result.QueryTree);

Console.WriteLine($"Final Answer ({result.TotalTokensUsed} tokens):");
Console.WriteLine(result.FinalAnswer);
```

## Success Criteria

1. ✅ **Backwards Compatible**: Existing RAG queries work unchanged
2. ✅ **Modular**: Each component testable independently
3. ✅ **Configurable**: Enable/disable per store, tune parameters
4. ✅ **Budget-Aware**: Respects token limits, doesn't run away
5. ✅ **Observable**: Query tree visible for debugging
6. ✅ **Graceful Fallback**: If recursion fails, falls back to single-shot

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Infinite recursion | High (cost explosion) | Hard limits on depth, branching factor, total tokens |
| Poor question decomposition | Medium (bad results) | Prompt engineering, fallback to single-shot |
| Breaking existing code | High (regression) | Backwards compatibility layer, opt-in per store |
| Performance overhead | Medium (latency) | Parallel execution where possible, caching |

## Next Steps

1. ✅ Create worktree for implementation
2. ✅ Implement Phase 1-3 (core + simple implementations)
3. ✅ Create PR for review
4. ⏸️ Iterate based on feedback
5. ⏸️ Complete Phases 4-8 in follow-up PRs if needed

---

**Sign-off**: Ready to implement. All dependencies verified. No blockers identified.
