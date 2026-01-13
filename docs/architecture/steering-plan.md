# Hazina LLM Steering - Implementation Plan

**Plan Date:** 2026-01-13
**Target Hazina Version:** 3.0
**Scope:** Add inference-time steering capabilities to Hazina framework

---

## Executive Summary

This document outlines a **phased implementation plan** for adding **LLM steering capabilities** to Hazina. Steering allows runtime modification of model behavior through **activation-level interventions**, going beyond traditional prompt engineering.

### Implementation Approach

**Philosophy:** Start simple, iterate, expand

1. **Phase 1:** Foundation - Core abstractions and data models
2. **Phase 2:** Local Provider Support - HuggingFace integration
3. **Phase 3:** Vector Storage - Persistence and retrieval
4. **Phase 4:** Vector Generation - Automated creation from examples
5. **Phase 5:** Advanced Features - Composition, optimization, marketplace

### Success Criteria

- ✅ Can apply steering vectors to local models (HuggingFace, Ollama)
- ✅ Can store and retrieve steering vectors
- ✅ Can generate vectors from example pairs
- ✅ Can blend multiple vectors with configurable coefficients
- ✅ Steering is provider-agnostic (graceful degradation for API providers)
- ✅ Maintains backward compatibility with existing `ILLMClient` interface

---

## Architecture Overview

### Component Hierarchy

```
Hazina.LLMs.Steering (NEW)
    │
    ├─► Core
    │   ├─► SteeringConfig.cs
    │   ├─► SteeringVector.cs
    │   ├─► LayerActivations.cs
    │   └─► ISteerableProvider.cs
    │
    ├─► Storage
    │   ├─► ISteeringVectorStore.cs
    │   ├─► FileSteeringVectorStore.cs
    │   └─► PostgresSteeringVectorStore.cs
    │
    ├─► Generation
    │   ├─► IVectorGenerator.cs
    │   ├─► ContrastiveVectorGenerator.cs
    │   └─► FineTunedVectorGenerator.cs
    │
    ├─► Analysis
    │   ├─► IActivationProfiler.cs
    │   ├─► LayerImportanceAnalyzer.cs
    │   └─► SteeringEffectivenessEvaluator.cs
    │
    └─► Composition
        ├─► VectorBlender.cs
        ├─► VectorNormalizer.cs
        └─► VectorOptimizer.cs

Hazina.LLMs.Providers (EXTEND)
    │
    ├─► Hazina.LLMs.HuggingFace (EXTEND)
    │   ├─► HuggingFaceSteerableWrapper.cs (NEW)
    │   └─► HuggingFaceActivationHooks.py (NEW)
    │
    └─► Hazina.LLMs.Ollama (EXTEND)
        └─► OllamaSteerableWrapper.cs (NEW)

Hazina.AI.Providers (EXTEND)
    └─► Core
        └─► ProviderOrchestrator.cs (EXTEND with steering)
```

---

## Phase 1: Foundation (2-3 weeks)

### Objectives

- ✅ Define core data models
- ✅ Create steering abstractions
- ✅ Extend provider interfaces
- ✅ Design configuration system

### 1.1 Core Data Models

**File:** `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Steering\Core\SteeringVector.cs`

```csharp
namespace Hazina.LLMs.Steering;

/// <summary>
/// Represents a steering vector that modifies model behavior at specific layers
/// </summary>
public class SteeringVector
{
    /// <summary>Unique identifier</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Human-readable name (e.g., "professional_tone")</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Category for organization (e.g., "tone", "domain", "persona")</summary>
    public string Category { get; set; } = "general";

    /// <summary>Detailed description of behavior modification</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Model family this vector is designed for (e.g., "gpt-2", "llama-2")</summary>
    public string ModelFamily { get; set; } = string.Empty;

    /// <summary>Model dimensionality (e.g., 4096 for GPT-2 XL)</summary>
    public int ModelDimensionality { get; set; }

    /// <summary>Layer-specific steering vectors (layer_index → vector)</summary>
    public Dictionary<int, float[]> LayerVectors { get; set; } = new();

    /// <summary>Recommended coefficient range [min, max]</summary>
    public (double Min, double Max) RecommendedCoefficient { get; set; } = (0.5, 1.5);

    /// <summary>Default coefficient if not specified</summary>
    public double DefaultCoefficient { get; set; } = 1.0;

    /// <summary>Additional metadata</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>Creation timestamp</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Last modified timestamp</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Version for tracking changes</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>Effectiveness metrics from evaluations</summary>
    public SteeringMetrics? Metrics { get; set; }
}

/// <summary>
/// Metrics tracking steering vector effectiveness
/// </summary>
public class SteeringMetrics
{
    public double AverageEffectiveness { get; set; }
    public double ConsistencyScore { get; set; }
    public int UsageCount { get; set; }
    public double AverageLatencyImpact { get; set; }
    public Dictionary<string, double> PerformanceMetrics { get; set; } = new();
}
```

**File:** `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Steering\Core\SteeringConfig.cs`

```csharp
namespace Hazina.LLMs.Steering;

/// <summary>
/// Configuration for applying steering vectors during inference
/// </summary>
public class SteeringConfig
{
    private List<SteeringApplication> _applications = new();

    /// <summary>All steering vectors to apply</summary>
    public IReadOnlyList<SteeringApplication> Applications => _applications;

    /// <summary>Whether to normalize vectors before application</summary>
    public bool NormalizeVectors { get; set; } = true;

    /// <summary>Whether to clip coefficients to recommended ranges</summary>
    public bool ClipCoefficients { get; set; } = true;

    /// <summary>Global coefficient multiplier (scales all vectors)</summary>
    public double GlobalMultiplier { get; set; } = 1.0;

    /// <summary>Add a steering vector to the configuration</summary>
    public SteeringConfig AddVector(
        SteeringVector vector,
        double coefficient,
        List<int>? targetLayers = null)
    {
        _applications.Add(new SteeringApplication
        {
            Vector = vector,
            Coefficient = coefficient,
            TargetLayers = targetLayers ?? vector.LayerVectors.Keys.ToList()
        });
        return this;
    }

    /// <summary>Add a steering vector by name (requires store lookup)</summary>
    public SteeringConfig AddVectorByName(
        string vectorName,
        double coefficient,
        List<int>? targetLayers = null)
    {
        _applications.Add(new SteeringApplication
        {
            VectorName = vectorName,
            Coefficient = coefficient,
            TargetLayers = targetLayers
        });
        return this;
    }

    /// <summary>Clear all steering applications</summary>
    public void Clear() => _applications.Clear();
}

/// <summary>
/// Individual steering vector application
/// </summary>
public class SteeringApplication
{
    public SteeringVector? Vector { get; set; }
    public string? VectorName { get; set; }
    public double Coefficient { get; set; } = 1.0;
    public List<int>? TargetLayers { get; set; }
}
```

**File:** `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Steering\Core\LayerActivations.cs`

```csharp
namespace Hazina.LLMs.Steering;

/// <summary>
/// Represents activations extracted from specific model layers
/// </summary>
public class LayerActivations
{
    /// <summary>Model identifier</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>Layer activations (layer_index → activation_vector)</summary>
    public Dictionary<int, float[]> Activations { get; set; } = new();

    /// <summary>Token IDs corresponding to activations</summary>
    public List<int> TokenIds { get; set; } = new();

    /// <summary>Token strings (for debugging)</summary>
    public List<string> Tokens { get; set; } = new();

    /// <summary>Attention weights (optional)</summary>
    public Dictionary<int, float[,]> AttentionWeights { get; set; } = new();

    /// <summary>Extraction timestamp</summary>
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
}
```

### 1.2 Provider Interface Extension

**File:** `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Steering\Core\ISteerableProvider.cs`

```csharp
namespace Hazina.LLMs.Steering;

/// <summary>
/// Extended interface for LLM providers that support activation steering
/// </summary>
public interface ISteerableProvider : ILLMClient
{
    /// <summary>Check if this provider supports activation steering</summary>
    bool SupportsActivationSteering { get; }

    /// <summary>Get list of layers available for steering</summary>
    List<int> GetSteerableLayers();

    /// <summary>Get model dimensionality for steering vector sizing</summary>
    int GetModelDimensionality();

    /// <summary>Get response with steering applied</summary>
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
        CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new();

    /// <summary>Extract layer activations for analysis</summary>
    Task<LayerActivations> ExtractActivations(
        List<HazinaChatMessage> messages,
        List<int> targetLayers,
        CancellationToken cancel);
}
```

### 1.3 Deliverables

- [ ] Create `Hazina.LLMs.Steering` project
- [ ] Implement all core data models
- [ ] Define `ISteerableProvider` interface
- [ ] Write unit tests for data models
- [ ] Document API with XML comments

---

## Phase 2: Local Provider Support (3-4 weeks)

### Objectives

- ✅ Implement steering for HuggingFace provider
- ✅ Add Python interop for Transformers library
- ✅ Create activation extraction pipeline
- ✅ Test with GPT-2 and Llama models

### 2.1 HuggingFace Steerable Wrapper

**File:** `C:\Projects\hazina\src\Core\LLMs.Providers\Hazina.LLMs.HuggingFace\Core\HuggingFaceSteerableWrapper.cs`

```csharp
namespace Hazina.LLMs.HuggingFace;

/// <summary>
/// HuggingFace wrapper with activation steering support
/// </summary>
public class HuggingFaceSteerableWrapper : HuggingFaceClientWrapper, ISteerableProvider
{
    private readonly PythonSteeringBridge _pythonBridge;

    public bool SupportsActivationSteering => true;

    public HuggingFaceSteerableWrapper(
        HuggingFaceConfig config,
        PythonSteeringBridge? pythonBridge = null)
        : base(config)
    {
        _pythonBridge = pythonBridge ?? new PythonSteeringBridge(config.Model);
    }

    public List<int> GetSteerableLayers()
    {
        return _pythonBridge.GetAvailableLayers();
    }

    public int GetModelDimensionality()
    {
        return _pythonBridge.GetModelDimensionality();
    }

    public async Task<LLMResponse<string>> GetResponseWithSteering(
        List<HazinaChatMessage> messages,
        SteeringConfig steeringConfig,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        // Convert messages to prompt
        var prompt = FormatMessages(messages);

        // Apply steering via Python bridge
        var response = await _pythonBridge.GenerateWithSteering(
            prompt,
            steeringConfig,
            cancel);

        return new LLMResponse<string>(
            response.Text,
            response.TokenUsage);
    }

    public async Task<LayerActivations> ExtractActivations(
        List<HazinaChatMessage> messages,
        List<int> targetLayers,
        CancellationToken cancel)
    {
        var prompt = FormatMessages(messages);
        return await _pythonBridge.ExtractActivations(prompt, targetLayers, cancel);
    }
}
```

### 2.2 Python Steering Bridge

**File:** `C:\Projects\hazina\src\Core\LLMs.Providers\Hazina.LLMs.HuggingFace\Python\steering_bridge.py`

```python
"""
Python bridge for HuggingFace model steering
"""
import torch
from transformers import AutoModelForCausalLM, AutoTokenizer
from typing import List, Dict, Optional
import numpy as np

class SteeringBridge:
    def __init__(self, model_name: str):
        self.model_name = model_name
        self.model = AutoModelForCausalLM.from_pretrained(model_name)
        self.tokenizer = AutoTokenizer.from_pretrained(model_name)
        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        self.model.to(self.device)

        # Store steering hooks
        self.steering_hooks = []

    def get_available_layers(self) -> List[int]:
        """Return list of transformer layer indices"""
        num_layers = len(self.model.transformer.h)
        return list(range(num_layers))

    def get_model_dimensionality(self) -> int:
        """Return hidden state dimensionality"""
        return self.model.config.n_embd

    def register_steering_hook(
        self,
        layer_idx: int,
        steering_vector: np.ndarray,
        coefficient: float
    ):
        """Register a forward hook to apply steering vector"""
        def steering_hook(module, input, output):
            # output[0] is the hidden states tensor
            # Shape: (batch_size, seq_len, hidden_dim)
            steering_tensor = torch.tensor(
                steering_vector * coefficient,
                dtype=output[0].dtype,
                device=output[0].device
            )
            # Add steering to all positions
            output[0] += steering_tensor
            return output

        layer = self.model.transformer.h[layer_idx]
        hook_handle = layer.register_forward_hook(steering_hook)
        self.steering_hooks.append(hook_handle)

    def clear_steering_hooks(self):
        """Remove all registered steering hooks"""
        for hook in self.steering_hooks:
            hook.remove()
        self.steering_hooks.clear()

    def generate_with_steering(
        self,
        prompt: str,
        steering_config: Dict,
        max_length: int = 100
    ) -> Dict:
        """Generate text with steering vectors applied"""
        # Clear previous hooks
        self.clear_steering_hooks()

        # Register new steering hooks
        for application in steering_config["applications"]:
            layer_vectors = application["layer_vectors"]
            coefficient = application["coefficient"]

            for layer_idx, vector in layer_vectors.items():
                self.register_steering_hook(
                    int(layer_idx),
                    np.array(vector),
                    coefficient
                )

        # Tokenize input
        inputs = self.tokenizer(prompt, return_tensors="pt").to(self.device)

        # Generate with steering active
        outputs = self.model.generate(
            **inputs,
            max_length=max_length,
            return_dict_in_generate=True,
            output_scores=True
        )

        # Decode output
        generated_text = self.tokenizer.decode(
            outputs.sequences[0],
            skip_special_tokens=True
        )

        # Clean up hooks
        self.clear_steering_hooks()

        return {
            "text": generated_text,
            "token_usage": {
                "prompt_tokens": inputs.input_ids.shape[1],
                "completion_tokens": outputs.sequences.shape[1] - inputs.input_ids.shape[1],
                "total_tokens": outputs.sequences.shape[1]
            }
        }

    def extract_activations(
        self,
        prompt: str,
        target_layers: List[int]
    ) -> Dict:
        """Extract activations from specified layers"""
        activations = {}

        def create_hook(layer_idx):
            def hook(module, input, output):
                activations[layer_idx] = output[0].detach().cpu().numpy()
            return hook

        # Register hooks
        hooks = []
        for layer_idx in target_layers:
            layer = self.model.transformer.h[layer_idx]
            hook_handle = layer.register_forward_hook(create_hook(layer_idx))
            hooks.append(hook_handle)

        # Run forward pass
        inputs = self.tokenizer(prompt, return_tensors="pt").to(self.device)
        with torch.no_grad():
            _ = self.model(**inputs)

        # Remove hooks
        for hook in hooks:
            hook.remove()

        # Tokenize for token info
        tokens = self.tokenizer.convert_ids_to_tokens(inputs.input_ids[0])

        return {
            "activations": {
                str(k): v.tolist() for k, v in activations.items()
            },
            "tokens": tokens,
            "token_ids": inputs.input_ids[0].tolist()
        }
```

**File:** `C:\Projects\hazina\src\Core\LLMs.Providers\Hazina.LLMs.HuggingFace\Core\PythonSteeringBridge.cs`

```csharp
namespace Hazina.LLMs.HuggingFace;

/// <summary>
/// C# wrapper for Python steering bridge
/// </summary>
public class PythonSteeringBridge
{
    private readonly Python.Runtime.PyObject _bridge;

    public PythonSteeringBridge(string modelName)
    {
        // Initialize Python runtime
        Python.Runtime.PythonEngine.Initialize();

        // Import steering module
        dynamic steering = Python.Runtime.Py.Import("steering_bridge");
        _bridge = steering.SteeringBridge(modelName);
    }

    public List<int> GetAvailableLayers()
    {
        dynamic layers = _bridge.get_available_layers();
        return layers.As<List<int>>();
    }

    public int GetModelDimensionality()
    {
        return _bridge.get_model_dimensionality().As<int>();
    }

    public async Task<(string Text, TokenUsageInfo TokenUsage)> GenerateWithSteering(
        string prompt,
        SteeringConfig config,
        CancellationToken cancel)
    {
        // Convert SteeringConfig to Python dict
        var pyConfig = ConvertConfigToPython(config);

        // Call Python bridge
        dynamic result = await Task.Run(() =>
            _bridge.generate_with_steering(prompt, pyConfig, 100));

        return (
            result["text"].As<string>(),
            new TokenUsageInfo
            {
                PromptTokens = result["token_usage"]["prompt_tokens"].As<int>(),
                CompletionTokens = result["token_usage"]["completion_tokens"].As<int>(),
                TotalTokens = result["token_usage"]["total_tokens"].As<int>()
            }
        );
    }

    public async Task<LayerActivations> ExtractActivations(
        string prompt,
        List<int> targetLayers,
        CancellationToken cancel)
    {
        dynamic result = await Task.Run(() =>
            _bridge.extract_activations(prompt, targetLayers));

        var activations = new LayerActivations
        {
            Tokens = result["tokens"].As<List<string>>(),
            TokenIds = result["token_ids"].As<List<int>>()
        };

        // Convert Python activations to C#
        dynamic pyActivations = result["activations"];
        foreach (var layerIdx in targetLayers)
        {
            var layerKey = layerIdx.ToString();
            activations.Activations[layerIdx] = pyActivations[layerKey].As<float[]>();
        }

        return activations;
    }

    private dynamic ConvertConfigToPython(SteeringConfig config)
    {
        // Convert to Python-compatible dictionary
        var applications = new List<object>();

        foreach (var app in config.Applications)
        {
            if (app.Vector == null) continue;

            applications.Add(new
            {
                layer_vectors = app.Vector.LayerVectors.ToDictionary(
                    kvp => kvp.Key.ToString(),
                    kvp => kvp.Value
                ),
                coefficient = app.Coefficient
            });
        }

        return Python.Runtime.PyDict.FromManagedObject(new
        {
            applications = applications
        });
    }
}
```

### 2.3 Deliverables

- [ ] Implement `HuggingFaceSteerableWrapper`
- [ ] Create Python steering bridge
- [ ] Add Python.NET integration
- [ ] Test with GPT-2 model
- [ ] Test with Llama-2 model
- [ ] Measure steering effectiveness
- [ ] Document Python dependencies

---

## Phase 3: Vector Storage (2 weeks)

### Objectives

- ✅ Design vector storage schema
- ✅ Implement file-based storage
- ✅ Implement PostgreSQL storage
- ✅ Add vector versioning
- ✅ Create management API

### 3.1 Storage Interface

**File:** `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Steering\Storage\ISteeringVectorStore.cs`

```csharp
namespace Hazina.LLMs.Steering.Storage;

/// <summary>
/// Interface for storing and retrieving steering vectors
/// </summary>
public interface ISteeringVectorStore
{
    /// <summary>Get vector by name</summary>
    Task<SteeringVector?> GetVectorAsync(string name, CancellationToken cancel = default);

    /// <summary>Get vector by ID</summary>
    Task<SteeringVector?> GetVectorByIdAsync(string id, CancellationToken cancel = default);

    /// <summary>Save or update vector</summary>
    Task SaveVectorAsync(SteeringVector vector, CancellationToken cancel = default);

    /// <summary>Delete vector</summary>
    Task DeleteVectorAsync(string id, CancellationToken cancel = default);

    /// <summary>List all vectors</summary>
    Task<List<SteeringVector>> ListVectorsAsync(
        string? category = null,
        string? modelFamily = null,
        CancellationToken cancel = default);

    /// <summary>Search vectors by name or description</summary>
    Task<List<SteeringVector>> SearchVectorsAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancel = default);

    /// <summary>Check if vector exists</summary>
    Task<bool> VectorExistsAsync(string name, CancellationToken cancel = default);

    /// <summary>Get vector versions (for versioned stores)</summary>
    Task<List<SteeringVector>> GetVectorVersionsAsync(
        string name,
        CancellationToken cancel = default);
}
```

### 3.2 File-Based Storage

**File:** `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Steering\Storage\FileSteeringVectorStore.cs`

```csharp
namespace Hazina.LLMs.Steering.Storage;

/// <summary>
/// File-based steering vector storage
/// </summary>
public class FileSteeringVectorStore : ISteeringVectorStore
{
    private readonly string _basePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileSteeringVectorStore(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(basePath);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<SteeringVector?> GetVectorAsync(
        string name,
        CancellationToken cancel = default)
    {
        var filePath = GetVectorFilePath(name);
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath, cancel);
        return JsonSerializer.Deserialize<SteeringVector>(json, _jsonOptions);
    }

    public async Task SaveVectorAsync(
        SteeringVector vector,
        CancellationToken cancel = default)
    {
        // Create category directory
        var categoryPath = Path.Combine(_basePath, vector.Category);
        Directory.CreateDirectory(categoryPath);

        // Save vector
        var filePath = GetVectorFilePath(vector.Name);
        vector.UpdatedAt = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(vector, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancel);
    }

    public async Task<List<SteeringVector>> ListVectorsAsync(
        string? category = null,
        string? modelFamily = null,
        CancellationToken cancel = default)
    {
        var vectors = new List<SteeringVector>();

        var searchPath = category != null
            ? Path.Combine(_basePath, category)
            : _basePath;

        if (!Directory.Exists(searchPath))
            return vectors;

        var files = Directory.GetFiles(
            searchPath,
            "*.json",
            SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var json = await File.ReadAllTextAsync(file, cancel);
            var vector = JsonSerializer.Deserialize<SteeringVector>(json, _jsonOptions);

            if (vector == null) continue;

            if (modelFamily != null &&
                !vector.ModelFamily.Equals(modelFamily, StringComparison.OrdinalIgnoreCase))
                continue;

            vectors.Add(vector);
        }

        return vectors;
    }

    private string GetVectorFilePath(string name)
    {
        // Sanitize name for filename
        var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_basePath, $"{safeName}.json");
    }

    // ... other interface implementations
}
```

### 3.3 PostgreSQL Storage

**File:** `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Steering\Storage\PostgresSteeringVectorStore.cs`

```csharp
namespace Hazina.LLMs.Steering.Storage;

/// <summary>
/// PostgreSQL-based steering vector storage with versioning
/// </summary>
public class PostgresSteeringVectorStore : ISteeringVectorStore
{
    private readonly string _connectionString;

    public PostgresSteeringVectorStore(string connectionString)
    {
        _connectionString = connectionString;
        InitializeDatabaseSchema().Wait();
    }

    private async Task InitializeDatabaseSchema()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS steering_vectors (
                id UUID PRIMARY KEY,
                name VARCHAR(255) NOT NULL,
                category VARCHAR(100),
                description TEXT,
                model_family VARCHAR(100),
                model_dimensionality INTEGER,
                layer_vectors JSONB NOT NULL,
                recommended_coefficient_min DOUBLE PRECISION,
                recommended_coefficient_max DOUBLE PRECISION,
                default_coefficient DOUBLE PRECISION,
                metadata JSONB,
                created_at TIMESTAMP DEFAULT NOW(),
                updated_at TIMESTAMP DEFAULT NOW(),
                version VARCHAR(50),
                is_deleted BOOLEAN DEFAULT FALSE
            );

            CREATE INDEX IF NOT EXISTS idx_steering_vectors_name
                ON steering_vectors(name);
            CREATE INDEX IF NOT EXISTS idx_steering_vectors_category
                ON steering_vectors(category);
            CREATE INDEX IF NOT EXISTS idx_steering_vectors_model_family
                ON steering_vectors(model_family);
        ";

        await using var cmd = new NpgsqlCommand(createTableSql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<SteeringVector?> GetVectorAsync(
        string name,
        CancellationToken cancel = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancel);

        var sql = @"
            SELECT * FROM steering_vectors
            WHERE name = @name AND is_deleted = FALSE
            ORDER BY updated_at DESC
            LIMIT 1
        ";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", name);

        await using var reader = await cmd.ExecuteReaderAsync(cancel);
        if (!await reader.ReadAsync(cancel))
            return null;

        return MapReaderToVector(reader);
    }

    // ... other interface implementations
}
```

### 3.4 Deliverables

- [ ] Implement `ISteeringVectorStore` interface
- [ ] Create `FileSteeringVectorStore`
- [ ] Create `PostgresSteeringVectorStore`
- [ ] Add vector versioning support
- [ ] Write unit tests for storage
- [ ] Create migration scripts
- [ ] Document storage schema

---

## Phase 4: Vector Generation (3-4 weeks)

### Objectives

- ✅ Implement contrastive vector generation
- ✅ Create example-based vector extraction
- ✅ Add vector optimization
- ✅ Build evaluation framework

### 4.1 Contrastive Vector Generator

**File:** `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Steering\Generation\ContrastiveVectorGenerator.cs`

```csharp
namespace Hazina.LLMs.Steering.Generation;

/// <summary>
/// Generates steering vectors by contrasting positive and negative examples
/// </summary>
public class ContrastiveVectorGenerator : IVectorGenerator
{
    private readonly ISteerableProvider _provider;
    private readonly ISteeringVectorStore _store;

    public async Task<SteeringVector> GenerateVectorAsync(
        string vectorName,
        string category,
        List<string> positiveExamples,
        List<string> negativeExamples,
        List<int>? targetLayers = null,
        CancellationToken cancel = default)
    {
        // 1. Extract activations for positive examples
        var positiveActivations = new List<LayerActivations>();
        foreach (var example in positiveExamples)
        {
            var messages = new List<HazinaChatMessage>
            {
                new() { Role = HazinaMessageRole.User, Text = example }
            };
            var layers = targetLayers ?? _provider.GetSteerableLayers();
            var activations = await _provider.ExtractActivations(messages, layers, cancel);
            positiveActivations.Add(activations);
        }

        // 2. Extract activations for negative examples
        var negativeActivations = new List<LayerActivations>();
        foreach (var example in negativeExamples)
        {
            var messages = new List<HazinaChatMessage>
            {
                new() { Role = HazinaMessageRole.User, Text = example }
            };
            var layers = targetLayers ?? _provider.GetSteerableLayers();
            var activations = await _provider.ExtractActivations(messages, layers, cancel);
            negativeActivations.Add(activations);
        }

        // 3. Compute mean activations
        var positiveMeans = ComputeMeanActivations(positiveActivations);
        var negativeMeans = ComputeMeanActivations(negativeActivations);

        // 4. Compute difference (steering direction)
        var steeringVectors = new Dictionary<int, float[]>();
        foreach (var layer in positiveMeans.Keys)
        {
            var diff = SubtractVectors(positiveMeans[layer], negativeMeans[layer]);
            steeringVectors[layer] = diff;
        }

        // 5. Normalize vectors
        foreach (var layer in steeringVectors.Keys)
        {
            steeringVectors[layer] = NormalizeVector(steeringVectors[layer]);
        }

        // 6. Create SteeringVector object
        var vector = new SteeringVector
        {
            Name = vectorName,
            Category = category,
            ModelFamily = GetModelFamily(_provider),
            ModelDimensionality = _provider.GetModelDimensionality(),
            LayerVectors = steeringVectors,
            Description = $"Generated from {positiveExamples.Count} positive and " +
                         $"{negativeExamples.Count} negative examples",
            Metadata = new Dictionary<string, object>
            {
                { "positive_examples", positiveExamples },
                { "negative_examples", negativeExamples },
                { "generation_method", "contrastive" }
            }
        };

        return vector;
    }

    private Dictionary<int, float[]> ComputeMeanActivations(
        List<LayerActivations> allActivations)
    {
        var means = new Dictionary<int, float[]>();

        foreach (var layerIdx in allActivations[0].Activations.Keys)
        {
            var dimensionality = allActivations[0].Activations[layerIdx].Length;
            var sum = new float[dimensionality];

            foreach (var activation in allActivations)
            {
                var layerVector = activation.Activations[layerIdx];
                for (int i = 0; i < dimensionality; i++)
                {
                    sum[i] += layerVector[i];
                }
            }

            for (int i = 0; i < dimensionality; i++)
            {
                sum[i] /= allActivations.Count;
            }

            means[layerIdx] = sum;
        }

        return means;
    }

    private float[] SubtractVectors(float[] a, float[] b)
    {
        var result = new float[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            result[i] = a[i] - b[i];
        }
        return result;
    }

    private float[] NormalizeVector(float[] vector)
    {
        var magnitude = (float)Math.Sqrt(vector.Sum(v => v * v));
        if (magnitude < 1e-8) return vector;

        var normalized = new float[vector.Length];
        for (int i = 0; i < vector.Length; i++)
        {
            normalized[i] = vector[i] / magnitude;
        }
        return normalized;
    }
}
```

### 4.2 Deliverables

- [ ] Implement `ContrastiveVectorGenerator`
- [ ] Add vector optimization (PCA, importance weighting)
- [ ] Create evaluation metrics (effectiveness, consistency)
- [ ] Build testing framework
- [ ] Document generation process
- [ ] Create example datasets

---

## Phase 5: Advanced Features (4-6 weeks)

### Objectives

- ✅ Vector composition (blending)
- ✅ Automatic coefficient optimization
- ✅ Steering effectiveness evaluation
- ✅ Integration with ProviderOrchestrator
- ✅ Web UI for vector management

### 5.1 Vector Blending

**File:** `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Steering\Composition\VectorBlender.cs`

```csharp
namespace Hazina.LLMs.Steering.Composition;

/// <summary>
/// Blends multiple steering vectors into a single composite vector
/// </summary>
public class VectorBlender
{
    public SteeringVector BlendVectors(
        List<(SteeringVector Vector, double Weight)> vectors,
        string compositeName,
        string description)
    {
        // Validate all vectors have same dimensionality
        var dimensionality = vectors[0].Vector.ModelDimensionality;
        if (vectors.Any(v => v.Vector.ModelDimensionality != dimensionality))
            throw new ArgumentException("All vectors must have same dimensionality");

        // Blend layer-by-layer
        var blendedLayers = new Dictionary<int, float[]>();
        var allLayers = vectors.SelectMany(v => v.Vector.LayerVectors.Keys).Distinct();

        foreach (var layer in allLayers)
        {
            var blended = new float[dimensionality];

            foreach (var (vector, weight) in vectors)
            {
                if (!vector.LayerVectors.ContainsKey(layer)) continue;

                var layerVector = vector.LayerVectors[layer];
                for (int i = 0; i < dimensionality; i++)
                {
                    blended[i] += layerVector[i] * (float)weight;
                }
            }

            blendedLayers[layer] = blended;
        }

        return new SteeringVector
        {
            Name = compositeName,
            Description = description,
            Category = "composite",
            ModelDimensionality = dimensionality,
            LayerVectors = blendedLayers,
            Metadata = new Dictionary<string, object>
            {
                { "composition", vectors.Select(v => new {
                    v.Vector.Name,
                    v.Weight
                }).ToList() }
            }
        };
    }
}
```

### 5.2 Deliverables

- [ ] Implement `VectorBlender`
- [ ] Create `VectorOptimizer` for coefficient tuning
- [ ] Add `SteeringEffectivenessEvaluator`
- [ ] Integrate steering into `ProviderOrchestrator`
- [ ] Build web UI for vector management
- [ ] Create marketplace for sharing vectors
- [ ] Write comprehensive documentation

---

## Integration Points

### 1. Provider Orchestrator Extension

**File:** `C:\Projects\hazina\src\Core\AI\Hazina.AI.Providers\Core\ProviderOrchestrator.cs` (extend)

```csharp
public class ProviderOrchestrator
{
    // NEW: Steering support
    public async Task<LLMResponse<string>> GetResponseWithSteering(
        List<HazinaChatMessage> messages,
        SteeringConfig steeringConfig,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        var selectedProvider = await SelectProviderAsync();

        // Check if provider supports steering
        if (selectedProvider is ISteerableProvider steerableProvider)
        {
            return await steerableProvider.GetResponseWithSteering(
                messages, steeringConfig, responseFormat,
                toolsContext, images, cancel);
        }

        // Fallback: use regular response (no steering)
        _logger.LogWarning(
            "Provider {Provider} does not support steering. Falling back to standard response.",
            selectedProvider.GetType().Name);

        return await selectedProvider.GetResponse(
            messages, responseFormat, toolsContext, images, cancel);
    }
}
```

### 2. Fluent API Extension

**File:** `C:\Projects\hazina\src\Core\AI\Hazina.AI.FluentAPI\Core\HazinaBuilder.cs` (extend)

```csharp
public class HazinaBuilder
{
    private SteeringConfig? _steeringConfig;

    // NEW: Fluent steering API
    public HazinaBuilder WithSteering(Action<SteeringConfig> configure)
    {
        _steeringConfig = new SteeringConfig();
        configure(_steeringConfig);
        return this;
    }

    public HazinaBuilder WithSteeringVector(string vectorName, double coefficient = 1.0)
    {
        _steeringConfig ??= new SteeringConfig();
        _steeringConfig.AddVectorByName(vectorName, coefficient);
        return this;
    }

    // Modified execution
    public async Task<string> ExecuteAsync()
    {
        if (_steeringConfig != null && _orchestrator != null)
        {
            var response = await _orchestrator.GetResponseWithSteering(
                _messages, _steeringConfig, _responseFormat, _toolsContext, _images, CancellationToken.None);
            return response.Result;
        }

        // ... existing logic
    }
}
```

**Usage Example:**
```csharp
// Fluent API with steering
var answer = await Hazina.AI()
    .WithProvider("huggingface")
    .WithSteeringVector("professional_tone", coefficient: 0.8)
    .WithSteeringVector("concise_style", coefficient: 0.6)
    .Ask("Explain quantum computing")
    .ExecuteAsync();
```

---

## Configuration Schema

### appsettings.json Extension

```json
{
  "Hazina": {
    "Steering": {
      "Enabled": true,
      "VectorStorePath": "C:\\HazinaVectors",
      "VectorStoreType": "File", // "File", "PostgreSQL"
      "PostgresConnectionString": "...",
      "DefaultVectorCategory": "general",
      "EnableVectorCaching": true,
      "CacheDurationMinutes": 60
    },
    "Providers": {
      "HuggingFace": {
        "EnableSteering": true,
        "PythonBridgePath": "./Python/steering_bridge.py"
      }
    }
  }
}
```

---

## Testing Strategy

### Unit Tests

1. **Data Model Tests**
   - SteeringVector serialization/deserialization
   - SteeringConfig fluent API
   - LayerActivations data integrity

2. **Storage Tests**
   - File-based CRUD operations
   - PostgreSQL CRUD operations
   - Versioning behavior
   - Search functionality

3. **Vector Generation Tests**
   - Contrastive vector generation
   - Vector normalization
   - Vector blending

### Integration Tests

1. **Provider Tests**
   - HuggingFace steering with GPT-2
   - Activation extraction accuracy
   - Steering effectiveness measurement

2. **End-to-End Tests**
   - Generate vector from examples
   - Store vector
   - Retrieve and apply vector
   - Validate behavior change

### Performance Tests

1. **Latency Impact**
   - Measure overhead of steering hooks
   - Compare steered vs. non-steered inference

2. **Memory Usage**
   - Vector storage memory footprint
   - Activation extraction memory

---

## Documentation Deliverables

1. **API Documentation**
   - XML comments for all public APIs
   - API reference guide

2. **User Guides**
   - Getting Started with Steering
   - Creating Custom Steering Vectors
   - Vector Management Best Practices

3. **Developer Guides**
   - Implementing Steerable Providers
   - Extending Vector Generation
   - Building Custom Vector Blenders

4. **Examples**
   - Tone modification examples
   - Domain specialization examples
   - Persona blending examples

---

## Migration & Backward Compatibility

### Principles

- ✅ **Non-breaking:** All steering features are **opt-in**
- ✅ **Graceful degradation:** Non-steerable providers work normally
- ✅ **Interface segregation:** `ISteerableProvider` extends `ILLMClient`

### Migration Path

1. **Existing Code:** Continues to work without changes
2. **Steering Adoption:** Gradual opt-in per provider
3. **Configuration:** Steering disabled by default

---

## Success Metrics

### Technical Metrics

- [ ] Steering latency overhead < 15%
- [ ] Vector generation time < 60 seconds
- [ ] Storage operations < 100ms (file-based)
- [ ] Support ≥2 local providers (HuggingFace, Ollama)

### Feature Completeness

- [ ] Can generate vectors from examples
- [ ] Can store/retrieve vectors
- [ ] Can apply vectors during inference
- [ ] Can blend multiple vectors
- [ ] Can evaluate vector effectiveness

### Quality Metrics

- [ ] Unit test coverage > 80%
- [ ] Integration test coverage > 60%
- [ ] Zero regressions in existing functionality
- [ ] Complete API documentation

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Python interop issues | High | Extensive testing, fallback to non-steering |
| Performance degradation | Medium | Profiling, optimization, caching |
| Vector portability | Medium | Model family tagging, validation |
| Provider API limitations | High | Focus on local models, document limitations |
| Storage schema changes | Low | Versioning, migration scripts |

---

## Timeline Estimate

| Phase | Duration | Dependencies |
|-------|----------|--------------|
| Phase 1: Foundation | 2-3 weeks | None |
| Phase 2: Local Providers | 3-4 weeks | Phase 1 |
| Phase 3: Vector Storage | 2 weeks | Phase 1 |
| Phase 4: Vector Generation | 3-4 weeks | Phase 2, Phase 3 |
| Phase 5: Advanced Features | 4-6 weeks | All previous |
| **Total** | **14-19 weeks** | |

---

## Next Steps

1. **Review** this plan with stakeholders
2. **Prioritize** phases based on business needs
3. **Allocate** development resources
4. **Set up** development environment (Python, HuggingFace)
5. **Begin** Phase 1 implementation

---

**Plan Status:** Draft
**Next Review:** TBD
**Implementation Start:** TBD
