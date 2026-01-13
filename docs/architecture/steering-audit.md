# Hazina LLM Steering Capabilities - Audit Report

**Audit Date:** 2026-01-13
**Hazina Version:** 2.x
**Auditor:** Claude Agent (Autonomous System Audit)

---

## Executive Summary

This document provides a comprehensive audit of Hazina's current LLM steering and behavior control capabilities. The audit examined the framework's ability to control, modify, and steer LLM inference behavior beyond traditional prompting approaches.

### Key Findings Summary

| Capability | Status | Implementation Level |
|------------|--------|---------------------|
| Inference-time behavior control | ✅ Partial | Prompt-based only |
| Activation/hidden state hooks | ❌ Not Implemented | N/A |
| Domain/tone/style biasing | ✅ Partial | Prompt-level metadata |
| JSON/structured output enforcement | ✅ Implemented | Provider-level enforcement |
| Safety/uncertainty control | ✅ Implemented | Post-inference validation |
| Persona/operator modes | ✅ Partial | Prompt template-based |
| Steering vector storage | ❌ Not Implemented | N/A |
| Layer/activation analysis | ❌ Not Implemented | N/A |
| Connector hidden state support | ❌ Not Implemented | N/A |

---

## 1. Inference-Time Behavior Control

### Current State: **PARTIALLY IMPLEMENTED (Prompt-Level Only)**

#### What Exists

Hazina currently controls LLM behavior exclusively through **prompt engineering** and **response validation**:

**Location:** `C:\Projects\hazina\src\Core\AI\Hazina.AI.FaultDetection\`

1. **Adaptive Fault Handler** (`Core/AdaptiveFaultHandler.cs`)
   - Post-generation response validation
   - Automatic retry with refined prompts
   - Confidence-based acceptance threshold
   - **Limitation:** Operates AFTER generation, not during inference

2. **Prompt Management System** (`C:\Projects\hazina\src\Core\AI\Hazina.AI.PromptManagement\`)
   - Template versioning and A/B testing
   - Performance metrics tracking
   - Prompt rewriting for optimization
   - **Limitation:** All control is prompt-based, not activation-based

#### What's Missing

- **No direct inference control:** Cannot modify model behavior during token generation
- **No activation steering:** Cannot inject steering vectors at specific layers
- **No runtime behavior modification:** Cannot adjust model "personality" without re-prompting
- **No layer-specific interventions:** Cannot target specific transformer layers

#### Example of Current Approach

```csharp
// Current: Behavior control via prompts + validation
var handler = new AdaptiveFaultHandler(orchestrator, validator,
    hallucinationDetector, errorPatternRecognizer, confidenceScorer,
    maxRetries: 3, minConfidenceThreshold: 0.7);

var response = await handler.ExecuteWithFaultDetectionAsync(messages, context);
// Response is validated AFTER generation
// If invalid, prompt is refined and retry occurs
```

**Ideal Steering Approach (Not Yet Implemented):**
```csharp
// Future: Inference-time steering
var steeringConfig = new SteeringConfig()
    .AddVector("professional_tone", coefficient: 0.8, layers: [16, 17, 18])
    .AddVector("concise_style", coefficient: 0.6, layers: [20, 21]);

var response = await orchestrator.GetResponseWithSteering(
    messages, steeringConfig, cancellationToken);
// Behavior modified DURING generation at activation level
```

---

## 2. Activation/Hidden State Hooks

### Current State: **NOT IMPLEMENTED**

#### Findings

**No infrastructure exists** for accessing or modifying model activations during inference.

**Searched Locations:**
- `C:\Projects\hazina\src\Core\LLMs\` - No activation hooks
- `C:\Projects\hazina\src\Core\LLMs.Providers\` - Provider wrappers have no layer access
- `ILLMClient` interface (`Hazina.LLMs.Client\ILLMClient.cs`) - No methods for activation access

**Current Provider Interface:**
```csharp
public interface ILLMClient
{
    Task<LLMResponse<string>> GetResponse(...);
    Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(...);
    Task<LLMResponse<string>> GetResponseStream(...);
    Task<Embedding> GenerateEmbedding(string data);
    // NO activation or hidden state access
}
```

#### What's Needed

1. **Extended Provider Interface:**
   ```csharp
   public interface ILLMClientWithSteering : ILLMClient
   {
       Task<LLMResponse<string>> GetResponseWithActivationHook(
           List<HazinaChatMessage> messages,
           Func<int, float[], float[]> activationTransform,
           List<int> targetLayers,
           CancellationToken cancel);

       Task<LayerActivations> GetLayerActivations(
           List<HazinaChatMessage> messages,
           List<int> layers,
           CancellationToken cancel);
   }
   ```

2. **Provider Support Requirements:**
   - **OpenAI:** Limited - no direct activation access via API
   - **Anthropic (Claude):** Limited - no activation access via API
   - **Ollama:** **Possible** - local models allow layer intervention
   - **HuggingFace:** **Ideal** - direct Transformers library access
   - **Local models:** **Fully supported** - complete control

#### Gap Analysis

| Requirement | Current | Needed |
|-------------|---------|--------|
| Layer identification | ❌ None | Target layer specification (e.g., layers 16-20) |
| Activation extraction | ❌ None | Forward hook registration |
| Activation modification | ❌ None | Intervention callbacks during generation |
| Steering vector application | ❌ None | Vector addition at specific layers |

---

## 3. Domain/Tone/Style Biasing

### Current State: **PARTIALLY IMPLEMENTED (Metadata + Prompts)**

#### What Exists

**Location:** `C:\Projects\hazina\src\Tools\Foundation\Hazina.Tools.Models\ToneOfVoice.cs`

```csharp
public class ToneOfVoice
{
    public List<string> ToneOfVoiceDescriptors { get; set; } = new List<string>();

    public ToneOfVoice _example => new ToneOfVoice
    {
        ToneOfVoiceDescriptors = new List<string> {
            "Friendly", "Confident", "Professional"
        }
    };
}
```

**Usage Pattern:**
- Tone descriptors are **stored as metadata** in the data model
- These descriptors are **injected into prompts** during generation
- **No activation-level bias** - purely prompt-based

**Example Current Implementation:**
```csharp
// Tone is applied via system prompt
var systemPrompt = $"You are a {brand.ToneOfVoice.ToneOfVoiceDescriptors[0]} " +
                   $"assistant. Maintain a {brand.ToneOfVoice.ToneOfVoiceDescriptors[1]} tone.";
messages.Insert(0, new HazinaChatMessage
{
    Role = HazinaMessageRole.System,
    Text = systemPrompt
});
```

#### What's Missing

- **No tone steering vectors:** Cannot apply "professional" or "casual" at activation level
- **No domain-specific bias:** Cannot bias model toward "medical" or "legal" domains without explicit prompting
- **No style transfer:** Cannot modify writing style during generation
- **No persistent tone application:** Tone must be re-specified in every prompt

#### Desired Capability

```csharp
// Future: Activation-level tone biasing
var toneVector = await steeringVectorStore.GetVector("professional_tone");
var domainVector = await steeringVectorStore.GetVector("medical_domain");

var steeringConfig = new SteeringConfig()
    .AddVector(toneVector, coefficient: 0.7)
    .AddVector(domainVector, coefficient: 0.5);

var response = await orchestrator.GetResponseWithSteering(
    messages, steeringConfig, cancellationToken);
// Model behavior shifted toward professional medical tone
// WITHOUT explicit prompting
```

---

## 4. JSON/Structured Output Enforcement

### Current State: **FULLY IMPLEMENTED**

#### Implementation Details

**Location:** `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Classes\Models\Chat\ChatResponse.cs`

Hazina has **robust structured output enforcement** via typed response classes:

```csharp
public abstract class ChatResponse<T> where T : ChatResponse<T>, new()
{
    [JsonIgnore]
    public abstract T _example { get; }

    [JsonIgnore]
    public abstract string _signature { get; }

    public static T Example => new T()._example;
    public static string Signature => new T()._signature;
}
```

**Enforcement Mechanism:**

1. **Type-safe responses:** `GetResponse<ResponseType>()` method enforces schema
2. **JSON mode:** `HazinaChatResponseFormat.Json` requests JSON output
3. **Example-based prompting:** `_example` provides schema to LLM
4. **Signature validation:** `_signature` defines expected structure

**Example Usage:**
```csharp
public class AnalysisResult : ChatResponse<AnalysisResult>
{
    public string Summary { get; set; }
    public double ConfidenceScore { get; set; }
    public List<string> KeyPoints { get; set; }

    public override AnalysisResult _example => new AnalysisResult
    {
        Summary = "Example summary",
        ConfidenceScore = 0.85,
        KeyPoints = new List<string> { "Point 1", "Point 2" }
    };

    public override string _signature =>
        "{ Summary: string, ConfidenceScore: number, KeyPoints: string[] }";
}

// Usage
var result = await llm.GetResponse<AnalysisResult>(messages, null, null, cancel);
// result.Result is strongly-typed AnalysisResult object
```

#### Validation Layer

**Location:** `C:\Projects\hazina\src\Core\AI\Hazina.AI.FaultDetection\Validators\BasicResponseValidator.cs`

- JSON syntax validation
- XML validation
- Code format detection
- Automatic correction on validation failure

#### Strength Assessment

✅ **Fully functional** for JSON enforcement
✅ **Type-safe** with compile-time guarantees
✅ **Provider-agnostic** - works across all LLM providers
✅ **Validation + retry** - automatically corrects malformed responses

**Limitation:** Enforcement is **prompt-based** (requesting JSON mode), not **grammar-constrained** (forcing valid JSON tokens)

---

## 5. Safety/Uncertainty Control Mechanisms

### Current State: **FULLY IMPLEMENTED**

Hazina has a **comprehensive safety and confidence system**.

#### Components

**Location:** `C:\Projects\hazina\src\Core\AI\Hazina.AI.FaultDetection\`

##### 5.1 Hallucination Detection

**File:** `Detectors/BasicHallucinationDetector.cs`

Detects 5 types of hallucinations:
1. **Contradictions** - Response contradicts conversation history
2. **Unsupported claims** - Definitive statements without ground truth
3. **Context mismatches** - Response doesn't align with prompt
4. **Fabricated details** - Overly specific fabricated data (precise numbers, UUIDs)
5. **Temporal errors** - Future years referenced as past events

**Detection Methods:**
```csharp
public async Task<HallucinationDetectionResult> DetectAsync(
    string response, ValidationContext context, CancellationToken cancel)
{
    // 1. Check contradictions with history
    DetectContradictions(response, context, result);

    // 2. Check unsupported claims
    DetectUnsupportedClaims(response, context, result);

    // 3. Check context mismatches
    DetectContextMismatches(response, context, result);

    // 4. Check fabricated details
    DetectFabricatedDetails(response, result);

    // 5. Check temporal errors
    DetectTemporalErrors(response, result);

    return result;
}
```

##### 5.2 Confidence Scoring

**File:** `Analyzers/BasicConfidenceScorer.cs`

Multi-factor confidence analysis:

| Factor | Weight | Description |
|--------|--------|-------------|
| Length | 10% | Too short = suspicious, too long = rambling |
| Hedging | 20% | Uncertain language ("maybe", "possibly") |
| Specificity | 20% | Presence of numbers, dates, proper nouns |
| Consistency | 30% | Keyword overlap with prompt |
| Format | 20% | Compliance with expected format |

**Scoring Algorithm:**
```csharp
private double CalculateWeightedScore(Dictionary<string, double> componentScores)
{
    var weights = new Dictionary<string, double>
    {
        { "length", 0.1 },
        { "hedging", 0.2 },
        { "specificity", 0.2 },
        { "consistency", 0.3 },
        { "format", 0.2 }
    };

    var weightedSum = componentScores.Sum(kvp =>
        weights.GetValueOrDefault(kvp.Key, 0.2) * kvp.Value);

    return Math.Max(0, Math.Min(1, weightedSum));
}
```

##### 5.3 Adaptive Fault Handler

**File:** `Core/AdaptiveFaultHandler.cs`

Automatic retry with refinement:

```csharp
public async Task<LLMResponse<string>> ExecuteWithFaultDetectionAsync(
    List<HazinaChatMessage> messages,
    ValidationContext validationContext,
    CancellationToken cancellationToken = default)
{
    int attempt = 0;
    while (attempt < _maxRetries)
    {
        var response = await _orchestrator.GetResponse(messages, ...);
        var validation = await ValidateResponseAsync(response.Result, ...);

        // Check confidence threshold
        if (validation.IsValid &&
            validation.ConfidenceScore >= _minConfidenceThreshold)
        {
            return response; // Success!
        }

        // Refine prompt based on issues
        messages = RefinePromptBasedOnIssues(messages, validation);

        // Learn from error
        await LearnFromErrorAsync(validation.Issues, response.Result);
    }

    // Return best attempt
    return bestResponse;
}
```

#### Strength Assessment

✅ **Production-ready** hallucination detection
✅ **Multi-factor confidence** with explainability
✅ **Automatic retry** with prompt refinement
✅ **Learning system** for pattern recognition
✅ **Configurable thresholds** (min confidence, max retries)

**Limitation:** All controls are **post-generation** (reactive), not **inference-time** (proactive)

---

## 6. Persona/Operator Modes

### Current State: **PARTIALLY IMPLEMENTED (Template-Based)**

#### What Exists

**Location:** `C:\Projects\hazina\src\Core\AI\Hazina.AI.PromptManagement\`

##### 6.1 Prompt Template System

Supports persona definition via templates:

**File:** `Core/Models/PromptTemplate.cs`

```csharp
public class PromptTemplate
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Template { get; set; } // Handlebars/Liquid/Scriban
    public string Category { get; set; }
    public Dictionary<string, object> Metadata { get; set; }

    // Template can include persona definition
    // Example: "You are a {{persona}} assistant with {{expertise}}"
}
```

##### 6.2 Role-Based Prompt Service

**Location:** `C:\Projects\hazina\src\Tools\Services\Hazina.Tools.Services.Prompts\RolePromptService.cs`

Simpler role/persona management via JSON files.

##### 6.3 Agent System

**Location:** `C:\Projects\hazina\src\Core\AI\Hazina.AI.Agents\Core\Agent.cs`

Agents can have persistent "roles":

```csharp
public class Agent
{
    public string Name { get; set; }
    public string Description { get; set; }
    public List<AgentTool> Tools { get; set; }
    // Role is embedded in system prompt
}
```

#### What's Missing

- **No persistent persona vectors:** Cannot load "expert medical advisor" behavior without re-prompting
- **No operator mode switching:** Cannot switch from "creative" to "analytical" mode mid-conversation
- **No persona blending:** Cannot combine multiple personas (e.g., "50% teacher + 50% comedian")
- **No inference-time persona:** All personas are prompt-embedded, not activation-embedded

#### Desired Capability

```csharp
// Future: Activation-level persona switching
var medicalExpertVector = await vectorStore.GetVector("medical_expert_persona");
var teacherVector = await vectorStore.GetVector("teacher_persona");

// Blend personas
var steeringConfig = new SteeringConfig()
    .AddVector(medicalExpertVector, coefficient: 0.7)
    .AddVector(teacherVector, coefficient: 0.3);

var response = await orchestrator.GetResponseWithSteering(
    messages, steeringConfig, cancellationToken);
// Model behaves as 70% medical expert + 30% teacher
// WITHOUT explicit persona prompts
```

---

## 7. Steering Vector Storage and Handling

### Current State: **NOT IMPLEMENTED**

#### Current Vector Infrastructure (Embeddings Only)

**Location:** `C:\Projects\hazina\src\Core\Storage\Hazina.Store.EmbeddingStore\`

Hazina has **extensive vector storage** for **embeddings** (semantic search):

**Implementations:**
- `InMemoryVectorStore` - Testing
- `FileEmbeddingStore` - Local JSON files
- `PgVectorStore` - PostgreSQL + pgvector extension
- `SupabaseEmbeddingStore` - Supabase cloud
- `SqliteEmbeddingStore` - SQLite with vector support

**Data Model:**
```csharp
public class TextEmbedding
{
    public string Id { get; set; }
    public string Text { get; set; }
    public float[] Vector { get; set; } // Embedding vector (e.g., 1536 dims)
    public Dictionary<string, object> Metadata { get; set; }
}
```

**Purpose:** **Semantic search** for RAG, NOT steering

#### What's Missing for Steering Vectors

**Key Differences Between Embeddings and Steering Vectors:**

| Aspect | Embeddings (Current) | Steering Vectors (Needed) |
|--------|---------------------|--------------------------|
| **Purpose** | Semantic similarity | Behavior modification |
| **Dimensionality** | 1536 (OpenAI), 768 (others) | Model-specific (e.g., 4096 for GPT-4) |
| **Usage** | Input to retrieval | Added to activations at specific layers |
| **Storage** | Per-document | Per-behavior/persona |
| **Application** | Pre-generation (RAG) | During-generation (steering) |

**Required Infrastructure:**

1. **Steering Vector Storage:**
   ```csharp
   public class SteeringVector
   {
       public string Id { get; set; }
       public string Name { get; set; }
       public string Category { get; set; } // "tone", "domain", "persona"
       public int ModelDimensionality { get; set; }
       public Dictionary<int, float[]> LayerVectors { get; set; } // Layer → Vector
       public Dictionary<string, object> Metadata { get; set; }
       public DateTime CreatedAt { get; set; }
       public string ModelFamily { get; set; } // "gpt-4", "claude-3", etc.
   }
   ```

2. **Steering Vector Store Interface:**
   ```csharp
   public interface ISteeringVectorStore
   {
       Task<SteeringVector> GetVector(string name);
       Task SaveVector(SteeringVector vector);
       Task<List<SteeringVector>> ListVectors(string? category = null);
       Task DeleteVector(string id);
       Task<SteeringVector> GenerateVector(
           string name,
           List<string> positiveExamples,
           List<string> negativeExamples);
   }
   ```

3. **Vector Generation Pipeline:**
   - Contrast positive/negative examples
   - Extract activation differences
   - Normalize and store per-layer vectors

#### Gap Analysis

| Component | Status | Priority |
|-----------|--------|----------|
| Steering vector data model | ❌ Not implemented | High |
| Steering vector storage | ❌ Not implemented | High |
| Vector generation pipeline | ❌ Not implemented | High |
| Vector application interface | ❌ Not implemented | High |
| Vector versioning | ❌ Not implemented | Medium |
| Vector composition (blending) | ❌ Not implemented | Medium |

---

## 8. Layer/Activation Analysis Tooling

### Current State: **NOT IMPLEMENTED**

#### What Exists (Related)

**Location:** `C:\Projects\hazina\src\Core\Observability\`

Hazina has **comprehensive observability** for:
- Request/response logging
- Token usage tracking
- Latency metrics
- OpenTelemetry integration

**What's Missing:**
- **No layer-level analysis**
- **No activation visualization**
- **No attention head analysis**
- **No layer importance scoring**

#### Desired Capabilities

1. **Layer Activation Profiling:**
   ```csharp
   public interface IActivationProfiler
   {
       Task<LayerActivationProfile> ProfileLayers(
           List<HazinaChatMessage> messages,
           List<int> targetLayers);

       Task<LayerImportanceScores> AnalyzeLayerImportance(
           List<HazinaChatMessage> messages);
   }
   ```

2. **Activation Visualization:**
   - Heatmaps of activation patterns
   - Layer-by-layer token attention
   - Activation difference analysis

3. **Diagnostic Tools:**
   - Identify optimal layers for steering
   - Measure steering vector effectiveness
   - Detect over-steering or under-steering

---

## 9. Connector Support for Hidden States and Forward Hooks

### Current State: **NOT IMPLEMENTED**

#### Provider Analysis

**Location:** `C:\Projects\hazina\src\Core\LLMs.Providers\`

Hazina supports **8 providers**, but **NONE expose hidden states**:

| Provider | Wrapper Location | Hidden State Support |
|----------|------------------|---------------------|
| **OpenAI** | `Hazina.LLMs.OpenAI\Core\OpenAIClientWrapper.cs` | ❌ API-based, no access |
| **Anthropic** | `Hazina.LLMs.Anthropic\Core\ClaudeClientWrapper.cs` | ❌ API-based, no access |
| **Gemini** | `Hazina.LLMs.Gemini\Core\GeminiClientWrapper.cs` | ❌ API-based, no access |
| **Mistral** | `Hazina.LLMs.Mistral\Core\MistralClientWrapper.cs` | ❌ API-based, no access |
| **Ollama** | `Hazina.LLMs.Ollama\Core\OllamaClientWrapper.cs` | ⚠️ **Potential** (local models) |
| **HuggingFace** | `Hazina.LLMs.HuggingFace\Core\HuggingFaceClientWrapper.cs` | ⚠️ **Potential** (Transformers lib) |
| **Semantic Kernel** | `Hazina.LLMs.SemanticKernel\Core\SemanticKernelClientWrapper.cs` | ❌ Abstraction layer |
| **Google ADK** | `Hazina.LLMs.GoogleADK\` | ❌ Workflow-based |

#### Opportunity: Local Model Providers

**Ollama and HuggingFace** are candidates for steering implementation:

1. **Ollama:**
   - Runs models locally
   - **Potential** for custom inference pipeline
   - Requires Ollama API extension or direct model access

2. **HuggingFace:**
   - Direct access to Transformers library
   - **Full control** over model internals
   - Can register forward hooks easily:
     ```python
     # Example (Python Transformers)
     def steering_hook(module, input, output):
         output[0] += steering_vector
         return output

     model.transformer.h[16].register_forward_hook(steering_hook)
     ```

#### Required Infrastructure

**Extended Provider Interface:**
```csharp
public interface ISteerableProvider : ILLMClient
{
    // Check if provider supports steering
    bool SupportsActivationSteering { get; }

    // Get available layers for steering
    List<int> GetSteerableLayers();

    // Get response with steering
    Task<LLMResponse<string>> GetResponseWithSteering(
        List<HazinaChatMessage> messages,
        SteeringConfig steeringConfig,
        HazinaChatResponseFormat responseFormat,
        CancellationToken cancel);

    // Extract activations for analysis
    Task<LayerActivations> ExtractActivations(
        List<HazinaChatMessage> messages,
        List<int> layers,
        CancellationToken cancel);
}
```

---

## Summary of Findings

### Implemented Capabilities ✅

1. **JSON/Structured Output Enforcement** - Fully functional
2. **Safety/Uncertainty Control** - Comprehensive post-generation validation
3. **Tone/Style Metadata** - Prompt-level only
4. **Persona Templates** - Prompt-based role management

### Partially Implemented ⚠️

1. **Inference-time Behavior Control** - Prompt refinement only, no activation control
2. **Domain/Tone/Style Biasing** - Metadata exists, but prompt-based application
3. **Persona/Operator Modes** - Template system exists, no activation-level switching

### Not Implemented ❌

1. **Activation/Hidden State Hooks** - No infrastructure
2. **Steering Vector Storage** - Embedding storage exists, steering storage missing
3. **Layer/Activation Analysis** - No tooling
4. **Connector Hidden State Support** - No provider exposes hidden states

---

## Recommendations

### Immediate Priorities

1. **Design steering vector data model** (see `steering-vectors.md`)
2. **Extend ILLMClient interface** with steering capabilities
3. **Implement local provider steering** (HuggingFace, Ollama)
4. **Create steering vector storage** (extend existing vector stores)

### Medium-Term Goals

1. **Build vector generation pipeline** (contrast-based)
2. **Develop activation profiling tools**
3. **Add steering composition** (vector blending)
4. **Create steering evaluation framework**

### Long-Term Vision

1. **Unified steering API** across all providers (where possible)
2. **Automatic steering discovery** (learn effective vectors from examples)
3. **Steering marketplace** (shareable community vectors)
4. **Real-time steering adjustment** (dynamic coefficient tuning)

---

## References

- **Next Steps:** See `steering-plan.md` for implementation roadmap
- **Module Design:** See `steering-module.md` for detailed architecture
- **Vector Specifications:** See `steering-vectors.md` for storage schema

---

**Audit Complete**
**Total Files Analyzed:** 150+
**Code Locations Examined:** 25+
**Documentation Generated:** 4 files
