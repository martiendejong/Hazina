# Hazina Architecture

**Updated for Hazina v2.0**

This document provides a comprehensive overview of Hazina's architecture, design principles, and component interactions.

---

## ⚠️ What's New in v2.0

### Architectural Improvements

- ✅ **Clean Code Architecture (PR #5)** - "30-Second Comprehension" design
  - Architectural tests (D34)
  - ILogger standardization (C30)
  - TestData patterns (D33)
  - See [CLEAN_CODE_*.md](.) files

- ✅ **Code Deduplication (PR #6)** - ~750 LOC eliminated
  - `HazinaConfigBase` - Base for all provider configs (~400 LOC)
  - `HazinaServiceBase` - Base for all services (~200 LOC)
  - `LLMProviderBase` - Base for all providers (~150 LOC)

### New Features

- ✅ **[3-Layer Tool Agent Architecture](TOOL_AGENT_ARCHITECTURE.md)** - 90% cost savings
- ✅ **[Context Compression Module](CONTEXT_COMPRESSION.md)** - 87% token reduction
- ✅ **[Google Drive Integration](GOOGLE_DRIVE_INTEGRATION.md)** - Cloud storage

See [IMPLEMENTATION-STATUS.md](../IMPLEMENTATION-STATUS.md) for complete feature list.

---

## 🎯 Design Principles

### 1. **Developer-First Experience**
- 4 lines to production
- Fluent API for discoverability
- Zero-config defaults with full customization

### 2. **Production-Ready by Default**
- Built-in monitoring and metrics
- Automatic cost tracking
- Health checks and circuit breakers
- Fault detection and recovery

### 3. **No Vendor Lock-In**
- Provider-agnostic abstraction
- Seamless provider switching
- Unified API across all LLMs

### 4. **Monorepo with Modularity**
- Focused solution files for different workflows
- Clear dependency boundaries
- Independent package deployments

---

## 📐 System Architecture

### High-Level Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Application Layer                             │
│                    (Your AI Application)                             │
└─────────────────────────────────────────────────────────────────────┘
                                  │
                                  │ Uses
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Hazina.AI.FluentAPI                               │
│                    (Developer Interface)                             │
│                                                                       │
│  QuickSetup ─→ Hazina.AI() ─→ WithProvider() ─→ Ask() ─→ Execute() │
└─────────────────────────────────────────────────────────────────────┘
        │              │              │              │              │
        │              │              │              │              │
        ▼              ▼              ▼              ▼              ▼
┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐
│Providers │   │   RAG    │   │ Agents   │   │Neurochain│   │ Fault    │
│          │   │          │   │          │   │          │   │Detection │
│OpenAI    │   │Indexing  │   │Tools     │   │3-Layer   │   │Halluc.   │
│Anthropic │   │Retrieval │   │Workflows │   │Reasoning │   │Detector  │
│Local LLMs│   │Reranking │   │Coord.    │   │Consensus │   │Validator │
└──────────┘   └──────────┘   └──────────┘   └──────────┘   └──────────┘
        │              │              │              │              │
        └──────────────┴──────────────┴──────────────┴──────────────┘
                                  │
                                  ▼
        ┌───────────────────────────────────────────────────────┐
        │           Infrastructure Layer                         │
        │                                                        │
        │  Storage  │  Security  │  Observability  │  Monitoring│
        │  Embeddings│  Auth     │  Logging        │  Metrics   │
        │  Documents │  Encryption│  Tracing        │  Health    │
        └───────────────────────────────────────────────────────┘
```

---

## 🧩 Core Components

### 1. Hazina.AI.FluentAPI

**Purpose**: Developer-first interface for all Hazina capabilities

**Key Classes**:
- `Hazina` - Static entry point
- `HazinaBuilder` - Fluent builder pattern
- `QuickSetup` - One-line setup helpers

**Example**:
```csharp
await Hazina.AI()
    .WithProvider("openai")
    .WithFaultDetection(0.9)
    .Ask("Question")
    .ExecuteAsync();
```

**Dependencies**:
- Hazina.AI.Providers
- Hazina.AI.FaultDetection
- Hazina.AI.Orchestration

---

### 2. Hazina.AI.Providers

**Purpose**: Multi-provider abstraction with automatic failover

**Key Components**:

```
ProviderOrchestrator
    │
    ├─► ProviderRegistry (manages providers)
    ├─► ProviderHealthMonitor (health checks)
    ├─► CostTracker (cost tracking)
    ├─► BudgetManager (budget alerts)
    ├─► ProviderSelector (selection strategies)
    ├─► CircuitBreaker (failure protection)
    └─► FailoverHandler (automatic failover)
```

**Selection Strategies**:
1. `Priority` - Ordered list
2. `LeastCost` - Cheapest option
3. `FastestResponse` - Fastest provider
4. `RoundRobin` - Load balancing
5. `Random` - Random selection
6. `Specific` - Named provider

**Health States**:
- `Healthy` - Operating normally
- `Degraded` - Increased latency/errors
- `Unhealthy` - Circuit open, unavailable

---

### 3. Hazina.Neurochain.Core

**Purpose**: Multi-layer reasoning with cross-validation

**Architecture**:

```
ReasoningContext
     │
     ▼
NeuroChainOrchestrator
     │
     ├─► FastReasoningLayer (GPT-3.5/Haiku)
     │      │
     │      └─► Quick analysis (<2s, 70-80% confidence)
     │
     ├─► DeepReasoningLayer (GPT-4/Sonnet)
     │      │
     │      └─► Thorough analysis (2-10s, 90-95% confidence)
     │
     └─► VerificationLayer (Independent model)
            │
            └─► Cross-validation (95-99% consensus)
```

**Execution Modes**:
- **Fast Only**: Single layer, <1s, 70-80% confidence
- **Early Stopping**: Stop when threshold reached
- **Parallel**: All layers simultaneously (60% faster)
- **Sequential**: Layer by layer (most thorough)

**Output**:
```csharp
NeuroChainResult
{
    FinalAnswer: string
    FinalConfidence: double (0-1)
    LayerResults: List<ReasoningResult>
    CrossValidation: CrossValidationResult
    TotalCost: decimal
    TotalDurationMs: long
}
```

---

### 4. Hazina.AI.RAG

**Purpose**: Retrieval-Augmented Generation

**Pipeline**:

```
Document
    │
    ├─► TextChunker (4 strategies)
    │      └─► Chunks with metadata
    │
    ├─► EmbeddingStore
    │      └─► Vector embeddings
    │
    └─► RAGEngine
           │
           ├─► Query → Semantic Search
           ├─► Retrieve → Top-K documents
           ├─► Rerank → Relevance scoring
           └─► Generate → LLM with context
```

**Chunking Strategies**:
1. `FixedSize` - Fixed character count
2. `Sentence` - Sentence boundaries
3. `Paragraph` - Paragraph boundaries
4. `Semantic` - Semantic coherence

**Reranking Strategies**:
1. `Similarity` - Cosine similarity only
2. `LLMBased` - LLM relevance scoring
3. `Hybrid` - Combined approach

---

### 5. Hazina.AI.Agents

**Purpose**: Agentic workflows and tool calling

**Architecture**:

```
Agent
  │
  ├─► Tools (extensible)
  │      └─► AgentTool implementations
  │
  ├─► WorkflowEngine
  │      │
  │      ├─► AgentTask
  │      ├─► Parallel (concurrent tasks)
  │      ├─► Conditional (if/then)
  │      └─► Loop (iterative refinement)
  │
  └─► MultiAgentCoordinator
         │
         ├─► Sequential (pipeline)
         ├─► Parallel (independent)
         ├─► Debate (discussion)
         └─► Hierarchical (manager-worker)
```

**Tool Calling**:
```csharp
// Agent detects tool calls
TOOL: CalculatorTool(expression=2+2)

// Tool executed
var result = tool.Execute(parameters);

// Result injected back to agent
Tool CalculatorTool returned: 4
```

---

### 6. Hazina.AI.FaultDetection

**Purpose**: Automatic validation and hallucination detection

**Components**:

```
AdaptiveFaultHandler
    │
    ├─► BasicResponseValidator
    │      └─► JSON, XML, Code format validation
    │
    ├─► BasicHallucinationDetector
    │      │
    │      ├─► FabricatedFact
    │      ├─► Contradiction
    │      ├─► ContextMismatch
    │      ├─► UnsupportedClaim
    │      ├─► AttributionError
    │      ├─► TemporalError
    │      └─► QuantitativeError
    │
    ├─► BasicErrorPatternRecognizer
    │      └─► Pattern learning from failures
    │
    └─► BasicConfidenceScorer
           │
           ├─► Length analysis
           ├─► Hedging detection
           ├─► Specificity scoring
           ├─► Consistency check
           └─► Format compliance
```

**Auto-Retry Flow**:
```
Response → Validate → Hallucination Check → Confidence Score
    │           │              │                    │
    OK          OK             OK                   OK → Return
    │           │              │                    │
    Fail        Fail           Fail                 Low
    │           │              │                    │
    └───────────┴──────────────┴────────────────────┘
                         │
                         ▼
              Refine Prompt → Retry (max 3x)
```

---

### 7. Hazina.CodeIntelligence

**Purpose**: Multi-file refactoring with architectural awareness

**Components**:

```
MultiFileRefactoringEngine
    │
    ├─► Dependency Analysis
    │      └─► Impact graph, circular detection
    │
    ├─► Risk Assessment
    │      └─► VeryLow → Critical
    │
    └─► Breaking Change Detection
```

```
LogicalInconsistencyDetector
    │
    ├─► NamingConvention
    ├─► Logic
    ├─► Architecture
    └─► Documentation
```

```
ProjectPatternLearner
    │
    ├─► Convention Discovery (70%+ consistency)
    ├─► Violation Detection
    └─► Improvement Suggestions
```

---

## 🗄️ Storage Layer

### Document Store

```
IDocumentStore
    │
    ├─► FileDocumentStore (local files)
    ├─► PostgresChunkStore (PostgreSQL)
    └─► SupabaseStoreProvider (cloud)
```

### Embedding Store

```
ITextEmbeddingStore
    │
    ├─► InMemoryVectorStore (testing)
    ├─► FileEmbeddingStore (local persistence)
    ├─► PgVectorStore (PostgreSQL + pgvector)
    └─► SupabaseEmbeddingStore (Supabase)
```

**Hybrid Mode**:
- Documents: Local file system
- Embeddings: Cloud database (Supabase/PostgreSQL)
- Best of both worlds: fast files + scalable search

---

## 📊 Production Layer

### Hazina.Production.Monitoring

```
MetricsCollector
    │
    ├─► Counter (increment only)
    ├─► Gauge (current value)
    └─► Histogram (distribution)

PerformanceProfiler
    │
    ├─► Operation tracking
    ├─► Statistics (Min, Max, Mean, P95, P99)
    └─► Markdown reports

DiagnosticsCollector
    │
    ├─► Health checks
    ├─► Memory monitoring
    ├─► CPU tracking
    └─► GC metrics
```

### Hazina.Observability.Core

```
Structured Logging
    │
    ├─► Request/Response logging
    ├─► LLM-specific logs
    └─► Serilog integration

OpenTelemetry
    │
    ├─► Traces
    ├─► Metrics
    └─► Logs
```

---

## 🔒 Security Layer

### Hazina.Security.Core

```
Security Features
    │
    ├─► Input validation
    ├─► Rate limiting
    ├─► API key management
    ├─► Prompt injection detection
    └─► Output sanitization
```

---

## 🔗 Component Dependencies

### Dependency Graph

```
Hazina.AI.FluentAPI
    │
    ├─► Hazina.AI.Providers
    │       │
    │       ├─► Hazina.LLMs.OpenAI
    │       ├─► Hazina.LLMs.Anthropic
    │       └─► Hazina.LLMs.Client (interface)
    │
    ├─► Hazina.AI.FaultDetection
    ├─► Hazina.AI.Orchestration
    ├─► Hazina.Neurochain.Core
    ├─► Hazina.AI.RAG
    │       │
    │       ├─► Hazina.Store.EmbeddingStore
    │       └─► Hazina.Store.DocumentStore
    │
    └─► Hazina.AI.Agents
```

### Zero-Dependency Packages

These can be used standalone:
- `Hazina.LLMs.OpenAI`
- `Hazina.LLMs.Anthropic`
- `Hazina.Store.EmbeddingStore`
- `Hazina.Store.DocumentStore`
- `Hazina.Production.Monitoring`

---

## 🚀 Scaling Strategy

### Horizontal Scaling

```
Load Balancer
    │
    ├─► Instance 1 (ProviderOrchestrator)
    ├─► Instance 2 (ProviderOrchestrator)
    └─► Instance 3 (ProviderOrchestrator)
            │
            └─► Shared PostgreSQL/Supabase
```

### Vertical Scaling

- Parallel layer execution (Neurochain)
- Concurrent agent workflows
- Batch embedding generation
- Connection pooling

### Cost Optimization

1. **Early Stopping**: Skip expensive layers when confidence sufficient
2. **Provider Selection**: Use `LeastCost` strategy
3. **Budget Limits**: Set hard caps with alerts
4. **Caching**: Reuse embeddings and responses

---

## 🧪 Testing Strategy

### Test Pyramid

```
    ┌───────────┐
    │    E2E    │  ← Integration tests
    ├───────────┤
    │   Unit    │  ← Component tests
    └───────────┘
```

### Test Organization

```
Tests/
├── Core/
│   ├── AI/
│   │   ├── Hazina.AI.Providers.Tests/
│   │   ├── Hazina.Neurochain.Core.Tests/
│   │   └── Hazina.AI.RAG.Tests/
│   └── LLMs/
│       └── Hazina.LLMs.OpenAI.Tests/
└── Tools/
    └── Hazina.Production.Monitoring.Tests/
```

---

## 📦 Deployment Options

### NuGet Packages

```bash
# Minimal
dotnet add package Hazina.AI.FluentAPI

# Full stack
dotnet add package Hazina.AI.FluentAPI
dotnet add package Hazina.AI.RAG
dotnet add package Hazina.AI.Agents
dotnet add package Hazina.Production.Monitoring
```

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
COPY bin/Release/net9.0/publish/ /app
ENTRYPOINT ["dotnet", "YourApp.dll"]
```

### Cloud Deployment

- **Azure**: App Service, Container Apps
- **AWS**: ECS, Lambda
- **GCP**: Cloud Run, GKE
- **Supabase**: Database + Auth + Storage

---

## 🔄 Development Workflow

### Solution Files for Different Workflows

| Workflow | Solution | Projects |
|----------|----------|----------|
| Getting started | `Hazina.QuickStart.sln` | 10 |
| AI development | `Hazina.AI.sln` | ~15 |
| Infrastructure | `Hazina.Core.sln` | ~20 |
| Tools/Services | `Hazina.Tools.sln` | ~20 |
| Applications | `Hazina.Apps.sln` | 14 |
| Full build | `Hazina.sln` | 62 |

### Build Optimization

**Directory.Build.props** enables:
- Incremental builds (only rebuild changed projects)
- Parallel builds (multi-threaded compilation)
- Shared compilation (reuse compiler processes)
- Reference assemblies (faster dependent builds)

**Result**: 83-95% faster incremental builds

---

## 📚 Further Reading

- [SOLUTIONS.md](../SOLUTIONS.md) - Solution file guide
- [RAG_GUIDE.md](RAG_GUIDE.md) - RAG implementation
- [AGENTS_GUIDE.md](AGENTS_GUIDE.md) - Agentic workflows
- [NEUROCHAIN_GUIDE.md](NEUROCHAIN_GUIDE.md) - Multi-layer reasoning
- [PRODUCTION_MONITORING_GUIDE.md](PRODUCTION_MONITORING_GUIDE.md) - Monitoring

---

**Last Updated**: 2026-01-05
