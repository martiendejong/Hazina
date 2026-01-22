# Hazina Native AI Stack

## Overview

This document describes the native C# AI/ML stack for Hazina, enabling local model inference, training, and advanced AI capabilities without Python dependencies.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         HAZINA.AI UNIFIED API                           │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │
│  │   Hazina    │  │   Hazina    │  │   Hazina    │  │   Hazina    │   │
│  │     AI      │  │     AI      │  │     AI      │  │     AI      │   │
│  │  .FluentAPI │  │.Orchestration│ │   .Memory   │  │    .RAG     │   │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    │               │               │
                    ▼               ▼               ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         MODEL EXECUTION LAYER                           │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │
│  │   Hazina    │  │   Hazina    │  │   Hazina    │  │   Hazina    │   │
│  │     AI      │  │     AI      │  │     AI      │  │     AI      │   │
│  │  .LocalLLM  │  │ .Inference  │  │  .Training  │  │   .Vision   │   │
│  │ (LLamaSharp)│  │(ONNX Runtime)│ │ (TorchSharp)│  │  (V-JEPA)   │   │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
                    │               │               │
                    ▼               ▼               ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         NATIVE BACKENDS                                 │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │
│  │  llama.cpp  │  │    ONNX     │  │  libtorch   │  │    CUDA     │   │
│  │    (C++)    │  │   Runtime   │  │    (C++)    │  │   cuDNN     │   │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
```

## New Projects

### 1. Hazina.AI.LocalLLM
**Purpose:** Run local LLMs (GGUF format) without API costs
**Backend:** LLamaSharp (C# bindings for llama.cpp)
**Implements:** `ILLMClient` (full compatibility with existing Hazina patterns)

**Capabilities:**
- Run Llama 3, Phi-4, DeepSeek, Qwen, Mistral locally
- CUDA/Metal/CPU acceleration
- Streaming responses
- Embeddings generation
- Context caching

### 2. Hazina.AI.Inference
**Purpose:** Run any ONNX model for inference
**Backend:** Microsoft.ML.OnnxRuntime
**Interface:** `IModelInference`

**Capabilities:**
- Load models from .onnx files
- Multi-backend support (CPU, CUDA, DirectML, TensorRT)
- Batched inference
- Model warm-up and caching
- Session pooling

### 3. Hazina.AI.Training
**Purpose:** Train and fine-tune models in C#
**Backend:** TorchSharp (.NET bindings for PyTorch)
**Interface:** `ITrainer`, `ILoRAAdapter`

**Capabilities:**
- Neural network construction
- LoRA adapter implementation
- Training loops with callbacks
- Checkpointing
- Gradient accumulation
- Mixed precision training

### 4. Hazina.AI.Vision
**Purpose:** Video/image understanding pipelines
**Backend:** ONNX Runtime + custom preprocessing
**Interface:** `IVisionPipeline`, `IVideoEncoder`

**Capabilities:**
- V-JEPA 2 video embeddings
- Frame extraction and tubelet processing
- Video QA with LLM integration
- Action recognition
- Object detection (YOLO, etc.)

## Interface Definitions

### IModelInference (New)
```csharp
public interface IModelInference : IDisposable
{
    Task<float[]> InferAsync(float[] input, CancellationToken ct = default);
    Task<float[][]> InferBatchAsync(float[][] inputs, CancellationToken ct = default);
    ModelMetadata Metadata { get; }
    void WarmUp();
}
```

### ITrainer (New)
```csharp
public interface ITrainer<TModel> where TModel : class
{
    Task<TrainingResult> TrainAsync(
        TModel model,
        IDataset dataset,
        TrainingConfig config,
        IProgress<TrainingProgress>? progress = null,
        CancellationToken ct = default);

    Task SaveCheckpointAsync(string path, CancellationToken ct = default);
    Task LoadCheckpointAsync(string path, CancellationToken ct = default);
}
```

### ILoRAAdapter (New)
```csharp
public interface ILoRAAdapter
{
    int Rank { get; }
    float Alpha { get; }

    void AttachTo(Module baseModel);
    void Detach();

    Task SaveAdapterAsync(string path, CancellationToken ct = default);
    Task<ILoRAAdapter> LoadAdapterAsync(string path, CancellationToken ct = default);

    Module MergeWithBase(Module baseModel);
}
```

### IVisionPipeline (New)
```csharp
public interface IVisionPipeline
{
    Task<float[]> EncodeImageAsync(byte[] imageData, CancellationToken ct = default);
    Task<float[]> EncodeVideoAsync(string videoPath, CancellationToken ct = default);
    Task<float[][]> EncodeFramesAsync(byte[][] frames, CancellationToken ct = default);
}
```

## Usage Examples

### Local LLM (No API, No Python)
```csharp
// Configure local LLM
var config = new LocalLLMConfig
{
    ModelPath = "models/llama-3.2-3b.Q4_K_M.gguf",
    GpuLayers = 32,
    ContextSize = 4096
};

// Create client implementing ILLMClient
ILLMClient llm = new LocalLLMClient(config);

// Use exactly like any other Hazina LLM provider
var messages = new List<HazinaChatMessage>
{
    new() { Role = HazinaMessageRole.User, Text = "Explain quantum computing" }
};

var response = await llm.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, ct);
Console.WriteLine(response.Result);
```

### ONNX Inference
```csharp
// Load any ONNX model
using var model = await OnnxModel.LoadAsync("models/v-jepa-2.onnx", new OnnxConfig
{
    ExecutionProvider = ExecutionProvider.CUDA,
    DeviceId = 0
});

// Run inference
var embeddings = await model.InferAsync(preprocessedFrames);
```

### LoRA Fine-tuning
```csharp
// Load base model
using var baseModel = await TorchModel.LoadAsync("models/llama-7b.pt");

// Create LoRA adapter
var lora = new LoRAAdapter(rank: 16, alpha: 32f);
lora.AttachTo(baseModel);

// Fine-tune on your data
var trainer = new LoRATrainer(new TrainingConfig
{
    LearningRate = 1e-4f,
    BatchSize = 4,
    Epochs = 3,
    GradientAccumulation = 8
});

await trainer.TrainAsync(baseModel, myDataset, progress: new Progress<TrainingProgress>(p =>
{
    Console.WriteLine($"Epoch {p.Epoch}, Loss: {p.Loss:F4}");
}));

// Save adapter (only ~10MB instead of 7GB)
await lora.SaveAdapterAsync("adapters/my-domain.pt");
```

### Video Understanding with V-JEPA 2
```csharp
// Create vision pipeline
var vision = new VisionPipeline(new VisionConfig
{
    EncoderModel = "models/v-jepa-2-encoder.onnx",
    FrameSize = (224, 224),
    FramesPerClip = 16
});

// Get video embeddings
var embeddings = await vision.EncodeVideoAsync("meeting.mp4");

// Combine with LLM for video QA
var llm = Hazina.CreateClient(provider: "local", model: "llama-3.2-3b");
var answer = await llm.GetResponse(new[]
{
    new HazinaChatMessage
    {
        Role = HazinaMessageRole.System,
        Text = $"Video context embedding: [{string.Join(",", embeddings.Take(100))}...]"
    },
    new HazinaChatMessage
    {
        Role = HazinaMessageRole.User,
        Text = "What decisions were made in this meeting?"
    }
}.ToList(), HazinaChatResponseFormat.Text, null, null, ct);
```

## NuGet Packages Required

| Package | Version | Purpose |
|---------|---------|---------|
| LLamaSharp | 0.19.0+ | llama.cpp bindings |
| LLamaSharp.Backend.Cuda12 | 0.19.0+ | CUDA support |
| Microsoft.ML.OnnxRuntime | 1.24.0+ | ONNX inference |
| Microsoft.ML.OnnxRuntime.Gpu | 1.24.0+ | CUDA for ONNX |
| TorchSharp | 0.105.0+ | PyTorch bindings |
| TorchSharp.cuda-windows | 0.105.0+ | CUDA for TorchSharp |

## Project Dependencies

```
Hazina.AI.LocalLLM
├── Hazina.LLMs.Client (ILLMClient interface)
├── Hazina.LLMs.Classes (message types)
└── LLamaSharp

Hazina.AI.Inference
├── Hazina.AI.Core (new - shared interfaces)
└── Microsoft.ML.OnnxRuntime

Hazina.AI.Training
├── Hazina.AI.Core
└── TorchSharp

Hazina.AI.Vision
├── Hazina.AI.Inference
├── Hazina.AI.Core
└── SixLabors.ImageSharp (image preprocessing)
```

## Implementation Priority

1. **Hazina.AI.LocalLLM** - Highest value, lowest effort (LLamaSharp is mature)
2. **Hazina.AI.Inference** - High value, low effort (ONNX Runtime is production-ready)
3. **Hazina.AI.Training** - High value, medium effort (TorchSharp + LoRA implementation)
4. **Hazina.AI.Vision** - High value, medium effort (requires V-JEPA ONNX export)

## State-of-the-Art Techniques to Implement

### Phase 1 (This PR)
- [x] Local LLM inference (LLamaSharp)
- [x] ONNX model inference
- [x] Basic TorchSharp integration

### Phase 2 (Future)
- [ ] LoRA fine-tuning
- [ ] QLoRA (4-bit quantization + LoRA)
- [ ] V-JEPA 2 video pipeline

### Phase 3 (Research)
- [ ] ENGRAM memory architecture
- [ ] MoE routing
- [ ] Speculative decoding
- [ ] Flash Attention integration

## Testing Strategy

1. **Unit tests** - Interface compliance, configuration validation
2. **Integration tests** - Model loading, inference correctness
3. **Benchmark tests** - Performance comparisons, memory usage
4. **Example projects** - Real-world usage patterns

---

*Created: 2026-01-21*
*Author: Claude Agent (agent-003)*
*Branch: agent-003-ai-native-stack*
