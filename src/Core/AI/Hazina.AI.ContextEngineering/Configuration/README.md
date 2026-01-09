# Context Engineering Configuration

Fully configurable policies for retrieval, scoring, and context packing.

## Overview

The Context Engineering system uses three types of policies:

1. **RetrievalPolicy**: Controls which retrievers to use and their parameters
2. **ScoringPolicy**: Controls fusion strategies and score boosting
3. **PackingPolicy**: Controls context assembly and formatting

These are combined in `ContextEngineConfig` for complete configuration.

## Quick Start

### Using Presets

```csharp
// Use a preset configuration
var config = ContextEngineConfig.SemanticFocused;

// Or customize a preset
var config = ContextEngineConfig.Default;
config.RetrievalPolicy.SemanticTopK = 10;
config.PackingPolicy.MaxTokens = 16000;
```

### Creating Custom Configuration

```csharp
var config = new ContextEngineConfig
{
    Name = "My Custom Config",
    RetrievalPolicy = new RetrievalPolicy
    {
        SemanticEnabled = true,
        SemanticTopK = 8,
        SemanticWeight = 0.7,

        FactsEnabled = true,
        FactsTopK = 5,
        FactsWeight = 0.3,

        MetadataEnabled = false
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
        Sections = new List<string> { "facts", "chunks", "query" },
        TargetLanguage = "en"
    },
    FinalTopK = 10
};

// Validate
var errors = config.Validate();
if (errors.Any())
{
    Console.WriteLine("Configuration errors:");
    errors.ForEach(Console.WriteLine);
}
```

### JSON Configuration

```csharp
// Save to JSON
config.ToFile("myconfig.json");

// Load from JSON
var config = ContextEngineConfig.FromFile("myconfig.json");

// Or from string
var json = config.ToJson();
var config2 = ContextEngineConfig.FromJson(json);
```

## Presets

### Default
Balanced mix of all retrievers with weighted fusion.

```csharp
var config = ContextEngineConfig.Default;
```

### SemanticFocused
Heavy emphasis on embedding-based semantic search.

```csharp
var config = ContextEngineConfig.SemanticFocused;
// Semantic: 80%, Facts: 20%, Metadata: off
```

### FactsFocused
Heavy emphasis on compact, relevant facts.

```csharp
var config = ContextEngineConfig.FactsFocused;
// Facts: 100%, Semantic: off, Metadata: off
```

### TagFocused
Optimized for tag-based filtering and relevance.

```csharp
var config = ContextEngineConfig.TagFocused;
// Tag boost power: 2.0 (strong boost for tag matches)
```

### RecencyFocused
Prioritizes recent content over older content.

```csharp
var config = ContextEngineConfig.RecencyFocused;
// Recency decay: 0.95 (minimal decay)
// Max age: 7 days
```

### Compact
Minimal context with facts and query only (4000 tokens).

```csharp
var config = ContextEngineConfig.Compact;
// Only facts + query, no scores/tags
```

### Comprehensive
Maximum context with all sections included (32000 tokens).

```csharp
var config = ContextEngineConfig.Comprehensive;
// All sections, all metadata, topK=20
```

## RetrievalPolicy

Controls which retrievers to use and their settings.

### Properties

```csharp
public class RetrievalPolicy
{
    // Semantic retrieval
    public bool SemanticEnabled { get; set; } = true;
    public int SemanticTopK { get; set; } = 8;
    public double SemanticWeight { get; set; } = 0.6;
    public double SemanticMinSimilarity { get; set; } = 0.7;

    // Facts retrieval
    public bool FactsEnabled { get; set; } = true;
    public int FactsTopK { get; set; } = 5;
    public double FactsWeight { get; set; } = 0.3;

    // Metadata retrieval
    public bool MetadataEnabled { get; set; } = true;
    public int MetadataTopK { get; set; } = 5;
    public double MetadataWeight { get; set; } = 0.1;
    public Dictionary<string, string>? MetadataFilters { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Types { get; set; }

    // ID lookup
    public bool LookupEnabled { get; set; } = false;
    public List<string>? LookupIds { get; set; }
}
```

### Example

```csharp
var policy = new RetrievalPolicy
{
    SemanticEnabled = true,
    SemanticTopK = 10,
    SemanticWeight = 0.5,

    FactsEnabled = true,
    FactsTopK = 8,
    FactsWeight = 0.3,
    Tags = new List<string> { "iot", "sensors" },

    MetadataEnabled = true,
    MetadataTopK = 5,
    MetadataWeight = 0.2,
    MetadataFilters = new Dictionary<string, string>
    {
        ["domain"] = "iot",
        ["priority"] = "high"
    }
};
```

## ScoringPolicy

Controls fusion strategies and score boosting.

### Fusion Strategies

#### WeightedSum
Combines scores with configured weights.

```csharp
final_score = w_semantic * score_semantic + w_facts * score_facts + w_metadata * score_metadata
```

**Use when**: You have clear preferences for retriever importance.

#### ReciprocalRankFusion (RRF)
Combines based on ranks, not scores.

```csharp
final_score = Σ(weight_i / (k + rank_i))
```

**Use when**: Different retrievers use incompatible scoring scales.

#### MaxScore
Takes maximum score across all retrievers.

```csharp
final_score = max(score_semantic, score_facts, score_metadata)
```

**Use when**: You want the highest confidence match from any source.

### Properties

```csharp
public class ScoringPolicy
{
    public FusionStrategy Strategy { get; set; } = FusionStrategy.WeightedSum;
    public double MinimumScore { get; set; } = 0.0;

    // Tag boosting
    public bool UseTagBoost { get; set; } = true;
    public double TagBoostPower { get; set; } = 1.2;

    // Recency boosting
    public bool UseRecencyBoost { get; set; } = false;
    public double RecencyDecayFactor { get; set; } = 0.9;
    public int RecencyMaxAgeDays { get; set; } = 30;

    // Score normalization
    public bool NormalizeScores { get; set; } = true;
}
```

## PackingPolicy

Controls context assembly and formatting.

### Properties

```csharp
public class PackingPolicy
{
    public int MaxTokens { get; set; } = 12000;
    public List<string> Sections { get; set; } = new() { "facts", "metadata", "chunks", "query" };

    public bool IncludeSourceInfo { get; set; } = true;
    public bool IncludeScores { get; set; } = true;
    public bool IncludeTags { get; set; } = true;

    public string TargetLanguage { get; set; } = "auto";

    public string SectionSeparator { get; set; } = "\n\n";
    public string ItemSeparator { get; set; } = "\n";
    public string SectionHeaderFormat { get; set; } = "[{0}]";

    public bool TrimToFit { get; set; } = true;
    public List<string> TrimPriority { get; set; } = new() { "query", "facts", "metadata", "tags", "chunks" };
}
```

### Sections

Valid section names:
- **`facts`**: Compact, relevant facts
- **`metadata`**: Document metadata and titles
- **`tags`**: All relevant tags
- **`chunks`**: Full text chunks from semantic search
- **`query`**: The original query

### Example Output

```
[FACTS]
- Building X has 24 sensors
- Sensor pipeline type is Modbus

[METADATA]
Tags: iot, sensors, configuration
Documents: 3 relevant documents found

[CHUNKS]
[Source: semantic] [Score: 0.92] To configure the sensor pipeline...
[Source: semantic] [Score: 0.87] Building X sensors communicate via...

[QUERY]
How do I configure the sensor pipeline for Building X?
```

## JSON Format

Example configuration file:

```json
{
  "Name": "Production Config",
  "Description": "Optimized for production queries",
  "RetrievalPolicy": {
    "SemanticEnabled": true,
    "SemanticTopK": 8,
    "SemanticWeight": 0.6,
    "SemanticMinSimilarity": 0.7,
    "FactsEnabled": true,
    "FactsTopK": 5,
    "FactsWeight": 0.3,
    "MetadataEnabled": true,
    "MetadataTopK": 5,
    "MetadataWeight": 0.1,
    "Tags": ["iot", "sensors"]
  },
  "ScoringPolicy": {
    "Strategy": "WeightedSum",
    "MinimumScore": 0.5,
    "UseTagBoost": true,
    "TagBoostPower": 1.5,
    "UseRecencyBoost": false,
    "NormalizeScores": true
  },
  "PackingPolicy": {
    "MaxTokens": 12000,
    "Sections": ["facts", "metadata", "chunks", "query"],
    "IncludeSourceInfo": true,
    "IncludeScores": true,
    "IncludeTags": true,
    "TargetLanguage": "auto",
    "TrimToFit": true
  },
  "FinalTopK": 10
}
```

## Validation

All policies support validation:

```csharp
var errors = config.Validate();
if (errors.Any())
{
    foreach (var error in errors)
    {
        Console.WriteLine($"❌ {error}");
    }
}
else
{
    Console.WriteLine("✅ Configuration is valid");
}
```

Common validation errors:
- No retrievers enabled
- Total weight of enabled retrievers = 0
- Invalid section names
- Out-of-range parameters (topK, weights, etc.)

## Language Independence

The configuration system is fully language-agnostic:

- Retrievers work with any content representation
- Scoring is purely numerical
- `TargetLanguage` in PackingPolicy only affects final LLM translation

To support multiple languages:

```csharp
// Store facts symbolically or in minimal NL
var fact = new Fact { Content = "building_X_sensors=24", ... };

// Configure target language for output
config.PackingPolicy.TargetLanguage = "nl"; // Dutch
config.PackingPolicy.TargetLanguage = "de"; // German
config.PackingPolicy.TargetLanguage = "auto"; // Auto-detect
```

## Best Practices

1. **Start with presets**: Use `ContextEngineConfig.Default` and customize
2. **Validate early**: Call `config.Validate()` before use
3. **Save configurations**: Use JSON files for production configs
4. **Weight tuning**: Adjust retriever weights based on query type
5. **Token budgets**: Set `MaxTokens` based on your LLM's context window
6. **Section ordering**: Put most important sections first (they're less likely to be trimmed)

## Related

- [Retrieval Layer](../Retrieval/README.md)
- [Fusion Engine](../Fusion/README.md)
- [Main README](../README.md)
