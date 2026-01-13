# Hazina.LLMs.Steering - Module Specification

**Module Name:** Hazina.LLMs.Steering
**Version:** 1.0.0
**Status:** Design Specification
**Last Updated:** 2026-01-13

---

## Module Overview

The **Hazina.LLMs.Steering** module provides **inference-time behavior control** for Large Language Models through **activation-level interventions**. This module enables developers to modify model behavior without prompt engineering by applying steering vectors at specific transformer layers during generation.

### Core Capabilities

1. **Activation Steering** - Apply vectors to model activations during inference
2. **Vector Storage** - Persist and retrieve steering vectors
3. **Vector Generation** - Create vectors from example pairs
4. **Vector Composition** - Blend multiple vectors for complex behaviors
5. **Analysis Tools** - Profile layers and measure steering effectiveness

### Design Principles

- **Provider-agnostic interfaces** - Works across different LLM providers (where supported)
- **Graceful degradation** - Falls back to standard responses for non-steerable providers
- **Non-breaking** - Fully backward compatible with existing Hazina code
- **Performance-conscious** - Minimal overhead for steering operations
- **Type-safe** - Strongly-typed APIs with compile-time guarantees

---

## Module Structure

```
Hazina.LLMs.Steering/
├── Core/
│   ├── SteeringVector.cs
│   ├── SteeringConfig.cs
│   ├── SteeringApplication.cs
│   ├── LayerActivations.cs
│   ├── ISteerableProvider.cs
│   ├── SteeringMetrics.cs
│   └── SteeringException.cs
│
├── Storage/
│   ├── ISteeringVectorStore.cs
│   ├── FileSteeringVectorStore.cs
│   ├── PostgresSteeringVectorStore.cs
│   ├── SteeringVectorCache.cs
│   └── VectorStorageException.cs
│
├── Generation/
│   ├── IVectorGenerator.cs
│   ├── ContrastiveVectorGenerator.cs
│   ├── FineTunedVectorGenerator.cs
│   ├── VectorGenerationConfig.cs
│   └── GenerationException.cs
│
├── Composition/
│   ├── VectorBlender.cs
│   ├── VectorNormalizer.cs
│   ├── VectorOptimizer.cs
│   └── CompositionStrategies/
│       ├── WeightedAverageStrategy.cs
│       ├── PCAProjectionStrategy.cs
│       └── ICompositionStrategy.cs
│
├── Analysis/
│   ├── IActivationProfiler.cs
│   ├── LayerImportanceAnalyzer.cs
│   ├── SteeringEffectivenessEvaluator.cs
│   ├── ActivationVisualizer.cs
│   └── Analysis/
│       ├── LayerActivationProfile.cs
│       ├── LayerImportanceScores.cs
│       └── EffectivenessReport.cs
│
├── Configuration/
│   ├── SteeringModuleConfig.cs
│   └── SteeringConfigurationExtensions.cs
│
└── Utilities/
    ├── VectorMath.cs
    ├── LayerSelector.cs
    └── ModelFamilyDetector.cs
```

---

## Component Specifications

### 1. Core Components

#### 1.1 SteeringVector

**Purpose:** Represents a steering vector with layer-specific activations

**Properties:**
```csharp
public class SteeringVector
{
    // Identity
    public string Id { get; set; }
    public string Name { get; set; }
    public string Category { get; set; }
    public string Description { get; set; }

    // Model compatibility
    public string ModelFamily { get; set; }
    public int ModelDimensionality { get; set; }

    // Steering data
    public Dictionary<int, float[]> LayerVectors { get; set; }

    // Configuration
    public (double Min, double Max) RecommendedCoefficient { get; set; }
    public double DefaultCoefficient { get; set; }

    // Metadata
    public Dictionary<string, object> Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Version { get; set; }

    // Metrics
    public SteeringMetrics? Metrics { get; set; }
}
```

**Methods:**
```csharp
public class SteeringVector
{
    // Get vector for specific layer
    public float[]? GetLayerVector(int layerIndex);

    // Validate vector integrity
    public ValidationResult Validate();

    // Get compatible layers
    public List<int> GetCompatibleLayers();

    // Clone vector
    public SteeringVector Clone();

    // Scale all vectors by coefficient
    public SteeringVector Scale(double coefficient);
}
```

**Validation Rules:**
- `LayerVectors` must not be empty
- All vectors must have same dimensionality as `ModelDimensionality`
- `Name` must be unique within category
- `RecommendedCoefficient.Min` < `RecommendedCoefficient.Max`
- `Version` must follow SemVer format

---

#### 1.2 SteeringConfig

**Purpose:** Configuration for applying multiple steering vectors

**Fluent API:**
```csharp
public class SteeringConfig
{
    // Add vector with explicit instance
    public SteeringConfig AddVector(
        SteeringVector vector,
        double coefficient = 1.0,
        List<int>? targetLayers = null);

    // Add vector by name (requires store lookup)
    public SteeringConfig AddVectorByName(
        string vectorName,
        double coefficient = 1.0,
        List<int>? targetLayers = null);

    // Remove vector
    public SteeringConfig RemoveVector(string vectorId);

    // Clear all vectors
    public void Clear();

    // Get resolved vectors (with store lookups)
    public async Task<List<ResolvedSteering>> ResolveAsync(
        ISteeringVectorStore store,
        CancellationToken cancel = default);
}

public class ResolvedSteering
{
    public SteeringVector Vector { get; set; }
    public double Coefficient { get; set; }
    public List<int> TargetLayers { get; set; }
}
```

**Configuration Options:**
```csharp
public class SteeringConfig
{
    // Whether to normalize vectors before application
    public bool NormalizeVectors { get; set; } = true;

    // Whether to clip coefficients to recommended ranges
    public bool ClipCoefficients { get; set; } = true;

    // Global multiplier for all coefficients
    public double GlobalMultiplier { get; set; } = 1.0;

    // Maximum number of vectors to apply (performance)
    public int MaxVectors { get; set; } = 10;

    // Merge strategy for overlapping layers
    public LayerMergeStrategy MergeStrategy { get; set; } =
        LayerMergeStrategy.WeightedSum;
}

public enum LayerMergeStrategy
{
    WeightedSum,    // Default: sum with coefficients
    Average,        // Average all vectors
    MaxMagnitude,   // Use vector with max magnitude
    PriorityBased   // First vector wins
}
```

---

#### 1.3 ISteerableProvider

**Purpose:** Interface for LLM providers that support activation steering

**Interface:**
```csharp
public interface ISteerableProvider : ILLMClient
{
    /// <summary>Check if provider supports activation steering</summary>
    bool SupportsActivationSteering { get; }

    /// <summary>Get available layers for steering</summary>
    List<int> GetSteerableLayers();

    /// <summary>Get model dimensionality</summary>
    int GetModelDimensionality();

    /// <summary>Get model family identifier</summary>
    string GetModelFamily();

    /// <summary>Get response with steering</summary>
    Task<LLMResponse<string>> GetResponseWithSteering(
        List<HazinaChatMessage> messages,
        SteeringConfig steeringConfig,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel);

    /// <summary>Get typed response with steering</summary>
    Task<LLMResponse<ResponseType?>> GetResponseWithSteering<ResponseType>(
        List<HazinaChatMessage> messages,
        SteeringConfig steeringConfig,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
        where ResponseType : ChatResponse<ResponseType>, new();

    /// <summary>Get streaming response with steering</summary>
    Task<LLMResponse<string>> GetResponseStreamWithSteering(
        List<HazinaChatMessage> messages,
        Action<string> onChunkReceived,
        SteeringConfig steeringConfig,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel);

    /// <summary>Extract layer activations</summary>
    Task<LayerActivations> ExtractActivations(
        List<HazinaChatMessage> messages,
        List<int> targetLayers,
        CancellationToken cancel);

    /// <summary>Test steering effectiveness</summary>
    Task<SteeringTestResult> TestSteering(
        List<HazinaChatMessage> messages,
        SteeringConfig steeringConfig,
        CancellationToken cancel);
}
```

**Implementation Requirements:**
1. **Activation Access:** Must have access to model's hidden states
2. **Forward Hooks:** Must support registering hooks during generation
3. **Layer Identification:** Must provide layer indices and names
4. **Thread Safety:** All methods must be thread-safe
5. **Cancellation Support:** Must respect cancellation tokens

---

### 2. Storage Components

#### 2.1 ISteeringVectorStore

**Purpose:** Interface for storing and retrieving steering vectors

**Interface:**
```csharp
public interface ISteeringVectorStore
{
    // Basic CRUD
    Task<SteeringVector?> GetVectorAsync(string name, CancellationToken cancel = default);
    Task<SteeringVector?> GetVectorByIdAsync(string id, CancellationToken cancel = default);
    Task SaveVectorAsync(SteeringVector vector, CancellationToken cancel = default);
    Task DeleteVectorAsync(string id, CancellationToken cancel = default);

    // Listing and search
    Task<List<SteeringVector>> ListVectorsAsync(
        string? category = null,
        string? modelFamily = null,
        CancellationToken cancel = default);

    Task<List<SteeringVector>> SearchVectorsAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancel = default);

    // Existence check
    Task<bool> VectorExistsAsync(string name, CancellationToken cancel = default);

    // Versioning
    Task<List<SteeringVector>> GetVectorVersionsAsync(
        string name,
        CancellationToken cancel = default);

    // Bulk operations
    Task SaveVectorsAsync(
        List<SteeringVector> vectors,
        CancellationToken cancel = default);

    Task DeleteVectorsAsync(
        List<string> ids,
        CancellationToken cancel = default);

    // Statistics
    Task<StoreStatistics> GetStatisticsAsync(CancellationToken cancel = default);
}

public class StoreStatistics
{
    public int TotalVectors { get; set; }
    public Dictionary<string, int> VectorsByCategory { get; set; }
    public Dictionary<string, int> VectorsByModelFamily { get; set; }
    public long TotalStorageBytes { get; set; }
    public DateTime LastModified { get; set; }
}
```

**Storage Implementations:**

| Implementation | Use Case | Performance | Features |
|----------------|----------|-------------|----------|
| `FileSteeringVectorStore` | Development, testing | Fast read/write | Simple, portable |
| `PostgresSteeringVectorStore` | Production | Scalable | Versioning, search |
| `SupabaseSteeringVectorStore` | Cloud | Scalable | Multi-tenant |
| `SqliteSteeringVectorStore` | Embedded apps | Lightweight | Single-file |

---

#### 2.2 FileSteeringVectorStore

**File Structure:**
```
VectorStore/
├── tone/
│   ├── professional_tone.json
│   ├── casual_tone.json
│   └── formal_tone.json
├── domain/
│   ├── medical_expert.json
│   ├── legal_advisor.json
│   └── technical_writer.json
├── persona/
│   ├── teacher.json
│   ├── comedian.json
│   └── analyst.json
└── composite/
    ├── professional_teacher.json
    └── casual_comedian.json
```

**JSON Format:**
```json
{
  "id": "uuid-here",
  "name": "professional_tone",
  "category": "tone",
  "description": "Increases professionalism and formality",
  "modelFamily": "gpt-2",
  "modelDimensionality": 768,
  "layerVectors": {
    "16": [0.123, -0.456, 0.789, ...],
    "17": [0.234, -0.567, 0.890, ...],
    "18": [0.345, -0.678, 0.901, ...]
  },
  "recommendedCoefficient": {
    "min": 0.5,
    "max": 1.5
  },
  "defaultCoefficient": 1.0,
  "metadata": {
    "generationMethod": "contrastive",
    "positiveExamples": ["..."],
    "negativeExamples": ["..."]
  },
  "createdAt": "2026-01-13T10:00:00Z",
  "updatedAt": "2026-01-13T10:00:00Z",
  "version": "1.0.0",
  "metrics": {
    "averageEffectiveness": 0.85,
    "consistencyScore": 0.92,
    "usageCount": 150,
    "averageLatencyImpact": 0.05
  }
}
```

---

#### 2.3 PostgresSteeringVectorStore

**Schema:**
```sql
CREATE TABLE steering_vectors (
    id UUID PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    category VARCHAR(100),
    description TEXT,
    model_family VARCHAR(100),
    model_dimensionality INTEGER NOT NULL,
    layer_vectors JSONB NOT NULL,
    recommended_coefficient_min DOUBLE PRECISION,
    recommended_coefficient_max DOUBLE PRECISION,
    default_coefficient DOUBLE PRECISION,
    metadata JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    version VARCHAR(50),
    is_deleted BOOLEAN DEFAULT FALSE,
    CONSTRAINT unique_name_version UNIQUE (name, version)
);

CREATE INDEX idx_steering_vectors_name ON steering_vectors(name);
CREATE INDEX idx_steering_vectors_category ON steering_vectors(category);
CREATE INDEX idx_steering_vectors_model_family ON steering_vectors(model_family);
CREATE INDEX idx_steering_vectors_created_at ON steering_vectors(created_at);
CREATE INDEX idx_steering_vectors_metadata ON steering_vectors USING GIN(metadata);
```

**Versioning Strategy:**
- Each save creates new row with incremented version
- Soft delete (is_deleted flag)
- Version format: `{major}.{minor}.{patch}`
- Default query returns latest version only

---

### 3. Vector Generation Components

#### 3.1 IVectorGenerator

**Purpose:** Interface for generating steering vectors from examples

**Interface:**
```csharp
public interface IVectorGenerator
{
    /// <summary>Generate steering vector from examples</summary>
    Task<SteeringVector> GenerateVectorAsync(
        string vectorName,
        string category,
        VectorGenerationConfig config,
        CancellationToken cancel = default);

    /// <summary>Generate from positive/negative pairs</summary>
    Task<SteeringVector> GenerateFromContrastAsync(
        string vectorName,
        string category,
        List<string> positiveExamples,
        List<string> negativeExamples,
        List<int>? targetLayers = null,
        CancellationToken cancel = default);

    /// <summary>Generate from single examples (maximizes activation)</summary>
    Task<SteeringVector> GenerateFromExamplesAsync(
        string vectorName,
        string category,
        List<string> examples,
        List<int>? targetLayers = null,
        CancellationToken cancel = default);

    /// <summary>Refine existing vector with additional examples</summary>
    Task<SteeringVector> RefineVectorAsync(
        SteeringVector existingVector,
        List<string> positiveExamples,
        List<string> negativeExamples,
        double learningRate = 0.1,
        CancellationToken cancel = default);
}
```

**Configuration:**
```csharp
public class VectorGenerationConfig
{
    public List<string> PositiveExamples { get; set; } = new();
    public List<string> NegativeExamples { get; set; } = new();
    public List<int>? TargetLayers { get; set; }
    public bool NormalizeVectors { get; set; } = true;
    public VectorGenerationMethod Method { get; set; } =
        VectorGenerationMethod.Contrastive;
    public int MaxExamplesPerBatch { get; set; } = 10;
    public bool UseMedianInsteadOfMean { get; set; } = false;
}

public enum VectorGenerationMethod
{
    Contrastive,        // Positive - Negative
    PositiveOnly,       // Maximize positive activations
    PCA,                // Principal component analysis
    FineTuned           // Use fine-tuned model difference
}
```

---

#### 3.2 ContrastiveVectorGenerator

**Algorithm:**
```
1. For each positive example:
   a. Extract activations at target layers
   b. Store in positive_activations[]

2. For each negative example:
   a. Extract activations at target layers
   b. Store in negative_activations[]

3. Compute mean activations:
   positive_mean = mean(positive_activations)
   negative_mean = mean(negative_activations)

4. Compute steering direction:
   steering_vector = positive_mean - negative_mean

5. Normalize:
   steering_vector = steering_vector / ||steering_vector||

6. Return SteeringVector with layer-specific vectors
```

**Optimization:**
- Batch extraction for performance
- Cache activations for reuse
- Parallel processing of examples
- Progressive refinement

---

### 4. Composition Components

#### 4.1 VectorBlender

**Purpose:** Blend multiple steering vectors into composite vector

**Methods:**
```csharp
public class VectorBlender
{
    /// <summary>Blend vectors with specified weights</summary>
    public SteeringVector BlendVectors(
        List<(SteeringVector Vector, double Weight)> vectors,
        string compositeName,
        string description,
        ICompositionStrategy? strategy = null);

    /// <summary>Blend with automatic weight optimization</summary>
    public async Task<SteeringVector> BlendWithOptimizationAsync(
        List<SteeringVector> vectors,
        List<string> testExamples,
        string compositeName,
        string description,
        ISteerableProvider provider,
        CancellationToken cancel = default);

    /// <summary>Create persona blend (equal weights)</summary>
    public SteeringVector CreatePersonaBlend(
        List<SteeringVector> personas,
        string compositeName);
}
```

**Composition Strategies:**

1. **WeightedAverageStrategy** (default)
   ```csharp
   blended[i] = Σ(vector[i] * weight) / Σ(weight)
   ```

2. **PCAProjectionStrategy**
   ```csharp
   // Project onto principal components
   // Reconstruct in lower-dimensional space
   ```

3. **MaxMagnitudeStrategy**
   ```csharp
   // For each dimension, use value with max magnitude
   blended[i] = argmax(|vector[i]|)
   ```

---

#### 4.2 VectorNormalizer

**Purpose:** Normalize vectors for consistent application

**Methods:**
```csharp
public class VectorNormalizer
{
    /// <summary>L2 normalization</summary>
    public float[] NormalizeL2(float[] vector);

    /// <summary>L1 normalization</summary>
    public float[] NormalizeL1(float[] vector);

    /// <summary>Min-max normalization</summary>
    public float[] NormalizeMinMax(float[] vector, float min = 0, float max = 1);

    /// <summary>Z-score normalization</summary>
    public float[] NormalizeZScore(float[] vector);

    /// <summary>Normalize all layers in vector</summary>
    public SteeringVector NormalizeAllLayers(
        SteeringVector vector,
        NormalizationMethod method = NormalizationMethod.L2);
}
```

---

### 5. Analysis Components

#### 5.1 IActivationProfiler

**Purpose:** Profile layer activations for analysis

**Interface:**
```csharp
public interface IActivationProfiler
{
    /// <summary>Profile activations across all layers</summary>
    Task<LayerActivationProfile> ProfileLayersAsync(
        ISteerableProvider provider,
        List<HazinaChatMessage> messages,
        CancellationToken cancel = default);

    /// <summary>Analyze layer importance for steering</summary>
    Task<LayerImportanceScores> AnalyzeLayerImportanceAsync(
        ISteerableProvider provider,
        List<HazinaChatMessage> messages,
        SteeringVector vector,
        CancellationToken cancel = default);

    /// <summary>Compare activations with and without steering</summary>
    Task<ActivationComparison> CompareActivationsAsync(
        ISteerableProvider provider,
        List<HazinaChatMessage> messages,
        SteeringConfig steeringConfig,
        CancellationToken cancel = default);
}
```

**Data Models:**
```csharp
public class LayerActivationProfile
{
    public Dictionary<int, LayerStats> LayerStatistics { get; set; }
    public List<int> MostActiveLayersDescriptor { get; set; }
    public double AverageActivationMagnitude { get; set; }
}

public class LayerStats
{
    public int LayerIndex { get; set; }
    public double Mean { get; set; }
    public double StandardDeviation { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Sparsity { get; set; } // % of near-zero values
}

public class LayerImportanceScores
{
    public Dictionary<int, double> ImportanceByLayer { get; set; }
    public List<int> TopLayersForSteering { get; set; }
    public string RecommendedLayerRange { get; set; }
}
```

---

#### 5.2 SteeringEffectivenessEvaluator

**Purpose:** Measure effectiveness of steering vectors

**Methods:**
```csharp
public class SteeringEffectivenessEvaluator
{
    /// <summary>Evaluate steering effectiveness on test examples</summary>
    public async Task<EffectivenessReport> EvaluateAsync(
        ISteerableProvider provider,
        SteeringVector vector,
        List<string> testExamples,
        EffectivenessMetric metric,
        CancellationToken cancel = default);

    /// <summary>Compare multiple vectors on same examples</summary>
    public async Task<ComparisonReport> CompareVectorsAsync(
        ISteerableProvider provider,
        List<SteeringVector> vectors,
        List<string> testExamples,
        CancellationToken cancel = default);

    /// <summary>Find optimal coefficient for vector</summary>
    public async Task<CoefficientOptimizationResult> FindOptimalCoefficientAsync(
        ISteerableProvider provider,
        SteeringVector vector,
        List<string> testExamples,
        double minCoefficient = 0.1,
        double maxCoefficient = 2.0,
        double step = 0.1,
        CancellationToken cancel = default);
}

public enum EffectivenessMetric
{
    BehaviorChange,      // Measure output difference
    TaskSuccess,         // Binary success on task
    UserRating,          // Manual evaluation
    SemanticSimilarity   // Similarity to target examples
}
```

---

## Configuration

### appsettings.json Schema

```json
{
  "Hazina": {
    "Steering": {
      "Enabled": true,
      "VectorStoreType": "File",
      "VectorStorePath": "C:\\HazinaVectors",
      "PostgresConnectionString": "Host=localhost;Database=hazina;...",
      "CacheDurationMinutes": 60,
      "EnableVectorCaching": true,
      "DefaultCategory": "general",
      "MaxVectorsPerRequest": 10,
      "LogSteeringApplications": true,
      "SteeringLogPath": "C:\\Logs\\Steering"
    }
  }
}
```

### Dependency Injection

```csharp
public static class SteeringServiceExtensions
{
    public static IServiceCollection AddHazinaSteering(
        this IServiceCollection services,
        Action<SteeringModuleConfig>? configure = null)
    {
        var config = new SteeringModuleConfig();
        configure?.Invoke(config);

        // Register store
        services.AddSingleton<ISteeringVectorStore>(sp =>
        {
            return config.VectorStoreType switch
            {
                VectorStoreType.File =>
                    new FileSteeringVectorStore(config.VectorStorePath),
                VectorStoreType.PostgreSQL =>
                    new PostgresSteeringVectorStore(config.PostgresConnectionString),
                _ => throw new NotSupportedException()
            };
        });

        // Register generators
        services.AddTransient<IVectorGenerator, ContrastiveVectorGenerator>();

        // Register utilities
        services.AddSingleton<VectorBlender>();
        services.AddSingleton<VectorNormalizer>();
        services.AddSingleton<IActivationProfiler, DefaultActivationProfiler>();
        services.AddSingleton<SteeringEffectivenessEvaluator>();

        return services;
    }
}
```

---

## Error Handling

### Exception Hierarchy

```
SteeringException (base)
    ├── VectorStorageException
    │   ├── VectorNotFoundException
    │   ├── VectorAlreadyExistsException
    │   └── StorageCorruptedException
    │
    ├── VectorGenerationException
    │   ├── InsufficientExamplesException
    │   ├── ActivationExtractionFailedException
    │   └── DimensionalityMismatchException
    │
    ├── SteeringApplicationException
    │   ├── ProviderNotSteerableException
    │   ├── LayerNotSupportedException
    │   └── CoefficientOutOfRangeException
    │
    └── VectorValidationException
        ├── InvalidDimensionalityException
        ├── InvalidLayerIndexException
        └── InvalidMetadataException
```

### Error Handling Examples

```csharp
try
{
    var vector = await store.GetVectorAsync("professional_tone");
    if (vector == null)
        throw new VectorNotFoundException("professional_tone");
}
catch (VectorNotFoundException ex)
{
    _logger.LogWarning("Vector not found: {VectorName}", ex.VectorName);
    // Fallback to default behavior
}
catch (StorageCorruptedException ex)
{
    _logger.LogError(ex, "Vector storage corrupted");
    // Attempt recovery or fail gracefully
}
```

---

## Performance Considerations

### Optimization Strategies

1. **Vector Caching**
   - Cache frequently-used vectors in memory
   - LRU eviction policy
   - Configurable cache size

2. **Batch Operations**
   - Batch activation extraction
   - Parallel example processing
   - Connection pooling for storage

3. **Lazy Loading**
   - Load layer vectors on-demand
   - Stream large vectors from storage
   - Incremental normalization

4. **Profiling**
   - Measure steering overhead
   - Track cache hit rates
   - Monitor storage latency

### Performance Targets

| Operation | Target | Acceptable |
|-----------|--------|------------|
| Vector lookup (cached) | < 1ms | < 5ms |
| Vector lookup (storage) | < 50ms | < 200ms |
| Steering overhead | < 10% | < 20% |
| Vector generation | < 60s | < 180s |
| Activation extraction | < 100ms | < 500ms |

---

## Security Considerations

### Access Control

```csharp
public interface ISteeringVectorAccessControl
{
    Task<bool> CanReadVectorAsync(string userId, string vectorId);
    Task<bool> CanWriteVectorAsync(string userId, string vectorId);
    Task<bool> CanDeleteVectorAsync(string userId, string vectorId);
    Task<bool> CanShareVectorAsync(string userId, string vectorId);
}
```

### Validation

- Sanitize vector names (prevent path traversal)
- Validate vector dimensionality before application
- Limit coefficient ranges to prevent extreme behavior
- Validate metadata schemas

### Audit Logging

```csharp
public class SteeringAuditLog
{
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; }
    public string Action { get; set; } // Create, Read, Update, Delete, Apply
    public string VectorId { get; set; }
    public string VectorName { get; set; }
    public Dictionary<string, object> Details { get; set; }
}
```

---

## Testing Strategy

### Unit Tests

- Data model serialization/deserialization
- Vector math operations (blending, normalization)
- Storage CRUD operations
- Configuration validation

### Integration Tests

- End-to-end vector generation
- Storage persistence across restarts
- Provider steering integration
- Multi-vector composition

### Performance Tests

- Steering latency benchmarks
- Storage throughput tests
- Cache performance tests
- Memory usage profiling

### Acceptance Tests

- Behavior modification verification
- Coefficient effectiveness tests
- Cross-provider compatibility
- Graceful degradation tests

---

## Documentation Requirements

### API Documentation

- XML comments for all public APIs
- Auto-generated API reference (DocFX)
- Code examples for common scenarios

### User Documentation

- Getting Started guide
- Creating Custom Vectors tutorial
- Troubleshooting guide
- Best Practices

### Developer Documentation

- Architecture overview
- Extending vector generators
- Custom composition strategies
- Provider implementation guide

---

## Backward Compatibility

### Guarantees

- ✅ Existing `ILLMClient` usage unaffected
- ✅ No breaking changes to provider interfaces
- ✅ Steering is opt-in (disabled by default)
- ✅ Graceful degradation for non-steerable providers

### Migration Path

```csharp
// Before: Standard usage
var response = await llm.GetResponse(messages, format, tools, images, cancel);

// After: Opt-in steering
if (llm is ISteerableProvider steerable)
{
    var config = new SteeringConfig()
        .AddVectorByName("professional_tone", 0.8);
    var response = await steerable.GetResponseWithSteering(
        messages, config, format, tools, images, cancel);
}
else
{
    // Fallback to standard
    var response = await llm.GetResponse(messages, format, tools, images, cancel);
}
```

---

## Future Enhancements

### Roadmap Items

1. **Automatic Steering Discovery** - Learn effective vectors from user feedback
2. **Steering Marketplace** - Share and download community vectors
3. **Real-time Coefficient Tuning** - Adjust steering during generation
4. **Multi-model Vectors** - Vectors that work across model families
5. **Steering Presets** - Pre-configured bundles for common use cases

---

**Module Status:** Design Complete
**Implementation Status:** Not Started
**Next Review:** TBD
