# Context Engine Orchestration

Complete end-to-end context engineering system.

## Overview

The **ContextEngineOrchestrator** is the main entry point for the context engineering system. It coordinates:

1. **Retrieval**: Runs configured retrievers (semantic, facts, metadata, lookup)
2. **Fusion**: Combines results using configured fusion strategy
3. **Boosting**: Applies tag and recency boosting
4. **Packing**: Assembles final context within token budget

## Quick Start

### Basic Usage

```csharp
// Register services
services.AddContextEngineering();

// Inject and use
public class MyService
{
    private readonly IContextEngine _contextEngine;

    public MyService(IContextEngine contextEngine)
    {
        _contextEngine = contextEngine;
    }

    public async Task<string> GetContextAsync(string query)
    {
        // Use default configuration
        var context = await _contextEngine.GetContextAsync(query);
        return context;
    }
}
```

### With Custom Configuration

```csharp
// Create custom config
var config = new ContextEngineConfig
{
    Name = "Production Config",
    RetrievalPolicy = new RetrievalPolicy
    {
        SemanticEnabled = true,
        SemanticTopK = 10,
        SemanticWeight = 0.7,
        FactsEnabled = true,
        FactsTopK = 5,
        FactsWeight = 0.3
    },
    ScoringPolicy = new ScoringPolicy
    {
        Strategy = FusionStrategy.WeightedSum,
        UseTagBoost = true,
        TagBoostPower = 1.5
    },
    PackingPolicy = new PackingPolicy
    {
        MaxTokens = 12000,
        Sections = new List<string> { "facts", "chunks", "query" }
    },
    FinalTopK = 10
};

// Use custom config
var context = await _contextEngine.GetContextAsync(query, config);
```

### With Query Embedding

```csharp
// Pre-compute embedding (e.g., for caching)
var embedding = await embeddingService.GetEmbeddingAsync(query);

// Pass to context engine
var context = await _contextEngine.GetContextAsync(query, config, embedding);
```

## Architecture

### Flow Diagram

```
Query → Orchestrator → [Retrievers] → Fusion → Boosting → Packing → Context
                         ↓                ↓         ↓          ↓
                     FactRetriever    Weighted   Tag      Token
                     Semantic         Sum/RRF/  Boost    Budget
                     Metadata         MaxScore  Recency  Trimming
                     Lookup                     Boost
```

### Components

#### IContextEngine

Main interface for context engineering.

**Methods**:
- `GetContextAsync(query, config, embedding)`: Full control with custom config
- `GetContextAsync(query, embedding)`: Use default configuration

#### ContextEngineOrchestrator

Main implementation that coordinates all components.

**Responsibilities**:
1. Validate configuration
2. Build retrieval query from config
3. Execute retrievers in parallel (when possible)
4. Fuse results using configured strategy
5. Apply boosting (tag, recency)
6. Filter by minimum score
7. Pack into final context
8. Handle errors and logging

#### IContextPacker

Interface for packing retrieval results into formatted context.

**Methods**:
- `PackAsync(results, query, policy)`: Pack results into context
- `EstimateTokens(text)`: Estimate token count

#### ContextPacker

Implementation that assembles context based on packing policy.

**Features**:
- Section-based assembly (facts, metadata, tags, chunks, query)
- Configurable formatting (headers, separators)
- Token budget management
- Automatic trimming to fit

#### SectionFormatter

Helper for formatting different section types.

**Section Types**:
- **facts**: Compact facts with optional scores/tags
- **metadata**: Document metadata and tag summaries
- **tags**: Tag frequency analysis
- **chunks**: Full text chunks from semantic search
- **query**: Original query

#### TokenBudgetManager

Helper for managing token limits and trimming.

**Features**:
- Priority-based trimming (trim least important sections first)
- Smart truncation (prefer line breaks)
- Exact token budget enforcement

## Examples

### Example 1: Semantic Search Only

```csharp
var config = ContextEngineConfig.SemanticFocused;
var context = await _contextEngine.GetContextAsync(
    "How do I configure sensor pipelines?",
    config);

// Output:
// [CHUNKS]
// [Source: semantic] [Score: 0.92] To configure the sensor pipeline...
// [Source: semantic] [Score: 0.87] Building X sensors communicate via...
//
// [QUERY]
// How do I configure sensor pipelines?
```

### Example 2: Facts + Semantic

```csharp
var config = ContextEngineConfig.Default;
config.PackingPolicy.Sections = new() { "facts", "chunks", "query" };

var context = await _contextEngine.GetContextAsync(
    "How many sensors are in Building X?",
    config);

// Output:
// [FACTS]
// - Building X has 24 sensors (score: 0.95)
// - Sensor pipeline type is Modbus (score: 0.88)
//
// [CHUNKS]
// [Source: semantic] [Score: 0.89] Building X contains 24 temperature sensors...
//
// [QUERY]
// How many sensors are in Building X?
```

### Example 3: Tag-Focused with Metadata

```csharp
var config = ContextEngineConfig.TagFocused;
config.RetrievalPolicy.Tags = new() { "iot", "sensors" };

var context = await _contextEngine.GetContextAsync(
    "Show me IoT sensor documentation",
    config);

// Output:
// [FACTS]
// - Building X has 24 sensors [tags: iot, sensors] (score: 1.15) <- tag boosted
//
// [METADATA]
// Tags: iot, sensors, configuration
// Documents: 5 relevant documents found
//
// [CHUNKS]
// [Source: semantic] [Score: 1.08] To configure IoT sensors... [Tags: iot, sensors]
//
// [QUERY]
// Show me IoT sensor documentation
```

### Example 4: Compact Context

```csharp
var config = ContextEngineConfig.Compact;
var context = await _contextEngine.GetContextAsync(
    "What is the sensor count?",
    config);

// Output (minimal, ~4000 tokens max):
// [FACTS]
// - Building X has 24 sensors
// - Sensor pipeline type is Modbus
//
// [QUERY]
// What is the sensor count?
```

### Example 5: Comprehensive Context

```csharp
var config = ContextEngineConfig.Comprehensive;
var context = await _contextEngine.GetContextAsync(
    "Tell me everything about Building X sensors",
    config);

// Output (maximum detail, ~32000 tokens):
// [FACTS]
// - Building X has 24 sensors (score: 0.95) [tags: iot, building-x]
// - Sensor pipeline type is Modbus (score: 0.88) [tags: iot, modbus]
// ... (10 facts)
//
// [METADATA]
// Tags: iot, sensors, building-x, modbus, configuration
// Documents: 15 relevant documents found
// - Building X Sensor Configuration Guide (score: 0.92)
// - Modbus Protocol Documentation (score: 0.85)
// ... (5 documents)
//
// [TAGS]
// Relevant tags (8 unique):
// - iot (appears in 12 results)
// - sensors (appears in 10 results)
// - building-x (appears in 8 results)
// ... (8 tags)
//
// [CHUNKS]
// [Source: semantic] [Score: 0.92] Building X contains 24 temperature sensors... [Tags: iot, building-x]
// [Source: semantic] [Score: 0.89] The Modbus protocol is used... [Tags: iot, modbus]
// ... (20 chunks)
//
// [QUERY]
// Tell me everything about Building X sensors
```

## Configuration Presets

### Default
Balanced mix, good for general-purpose queries.

```csharp
var config = ContextEngineConfig.Default;
```

### SemanticFocused
Heavy emphasis on embedding-based search.

```csharp
var config = ContextEngineConfig.SemanticFocused;
// 80% semantic, 20% facts
```

### FactsFocused
Only compact facts, no semantic search.

```csharp
var config = ContextEngineConfig.FactsFocused;
// 100% facts, minimal context
```

### TagFocused
Strong tag matching boost.

```csharp
var config = ContextEngineConfig.TagFocused;
// Tag boost power: 2.0 (strong boost for tag matches)
```

### RecencyFocused
Recent content prioritized.

```csharp
var config = ContextEngineConfig.RecencyFocused;
// Recency decay: 0.95 (minimal decay)
// Max age: 7 days
```

### Compact
Minimal tokens (4000), only facts + query.

```csharp
var config = ContextEngineConfig.Compact;
// 4000 tokens max, no scores/tags
```

### Comprehensive
Maximum tokens (32000), all sections.

```csharp
var config = ContextEngineConfig.Comprehensive;
// 32000 tokens, all sections, topK=20
```

## Token Budget Management

The packing system automatically manages token budgets:

1. **Estimate tokens**: ~4 chars per token (GPT-style)
2. **Assemble sections**: Build all sections
3. **Check budget**: Compare to `MaxTokens`
4. **Trim if needed**: Remove or truncate sections based on `TrimPriority`

### Trim Priority

Default priority (trim in this order):
1. `chunks` (trimmed first)
2. `tags`
3. `metadata`
4. `facts`
5. `query` (never trimmed)

Customize:
```csharp
config.PackingPolicy.TrimPriority = new() { "query", "facts", "metadata", "chunks", "tags" };
```

## Error Handling

### Configuration Validation

```csharp
try
{
    var context = await _contextEngine.GetContextAsync(query, config);
}
catch (InvalidOperationException ex)
{
    // Invalid configuration
    Console.WriteLine($"Config error: {ex.Message}");
}
```

### Token Budget Exceeded

```csharp
config.PackingPolicy.TrimToFit = false; // Throw instead of trim

try
{
    var context = await _contextEngine.GetContextAsync(query, config);
}
catch (InvalidOperationException ex)
{
    // Context exceeds MaxTokens
    Console.WriteLine($"Token budget exceeded: {ex.Message}");
}
```

## Logging

The orchestrator logs at multiple levels:

```csharp
// Information: High-level flow
_logger.LogInformation("Starting context engineering for query: {Query}", query);
_logger.LogInformation("Fusion complete: {Count} final results", fusedResults.Count);
_logger.LogInformation("Context engineering complete: {Tokens} tokens", tokens);

// Debug: Detailed retrieval counts
_logger.LogDebug("Semantic retrieval: {Count} results", results.Count);
_logger.LogDebug("Facts retrieval: {Count} results", results.Count);
_logger.LogDebug("After minimum score filter: {Count} results", results.Count);

// Warning: Issues
_logger.LogWarning("No retrieval results obtained");
```

Configure logging:
```csharp
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});
```

## Best Practices

1. **Start with presets**: Use built-in configs and customize from there
2. **Cache embeddings**: Pre-compute query embeddings for repeated queries
3. **Validate configs**: Always validate before use
4. **Monitor tokens**: Log final token counts to tune `MaxTokens`
5. **Use appropriate configs**: Match config to query type (facts vs semantic)
6. **Set minimum scores**: Filter low-quality results with `MinimumScore`
7. **Configure trim priority**: Ensure most important sections are preserved

## Integration with RAGEngine

```csharp
public class HybridRAGService
{
    private readonly IContextEngine _contextEngine;
    private readonly IRagEngine _ragEngine;

    public async Task<string> AskAsync(string question)
    {
        // Get enriched context from context engineering
        var context = await _contextEngine.GetContextAsync(question);

        // Use with RAG engine
        var answer = await _ragEngine.AskAsync(question, context);

        return answer;
    }
}
```

## Related

- [Configuration](../Configuration/README.md)
- [Retrieval Layer](../Retrieval/README.md)
- [Fusion Engine](../Fusion/README.md)
- [Main README](../README.md)
