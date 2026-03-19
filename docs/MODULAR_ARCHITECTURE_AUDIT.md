# Hazina Modular Architecture Audit

**Phase 1: Comprehensive Architecture Documentation**
**Version:** 2.0
**Date:** 2026-03-19
**Total Projects:** 172
**Published NuGet Packages:** 99

## Executive Summary

This document provides a comprehensive audit of the Hazina framework architecture as part of the modular refactoring initiative (Phase 1). The analysis covers all 172 C# projects, their dependencies, layering, and identifies opportunities for improved modularity.

### Key Findings

1. **Well-Defined Foundation** - 12 core projects form a stable foundation with 49+ dependents on key abstractions
2. **Clear Layering** - 5 distinct architectural layers from Foundation to Applications
3. **Provider Flexibility** - 8 LLM providers implementing common interface (zero cross-dependencies)
4. **Some Tight Coupling** - AgentFactory (10 deps), RAG→Neurochain, FluentAPI→All Providers
5. **Opportunity for Modularization** - Services layer and agent systems can be further split

---

## Architecture Layers

### Layer 1: Foundation (12 projects)

**Core abstractions with zero Hazina dependencies - most stable**

| Project | Dependents | Purpose |
|---------|-----------|---------|
| Hazina.LLMs.Client | 49 | Core LLM client interface (ILLMClient) |
| Hazina.LLMs.Classes | 36 | Shared DTOs (ChatMessage, ChatResponse) |
| Hazina.Tools.Data | 26 | Data access patterns |
| Hazina.Tools.Models | 25 | Domain models |
| Hazina.LLMs.Helpers | 20 | Token counting, formatting |
| Hazina.Tools.Core | 19 | Base tool infrastructure |
| Hazina.AI.Providers | 19 | Provider orchestration |
| Hazina.Store.EmbeddingStore | 19 | Vector storage abstraction |
| Hazina.Store.DocumentStore | 16 | Document storage |
| Hazina.Store.FactsStore | - | Knowledge storage |
| Hazina.Store.Sqlite | - | SQLite implementation |
| Hazina.LLMClientTools | 8 | Tool helpers |

**Characteristics:**
- Stable APIs (major version changes only)
- No Hazina project dependencies
- Used by 90%+ of codebase

### Layer 2: Core AI (28 projects)

#### AI Infrastructure
- `Hazina.AI.Core` (9 deps) - Base AI abstractions
- `Hazina.AI.Orchestration` - Request routing, failover
- `Hazina.AI.FluentAPI` - Fluent configuration API
- `Hazina.AI.Providers` (19 deps) - Multi-provider coordination

#### RAG & Context
- `Hazina.AI.RAG` (9 deps) - Retrieval-Augmented Generation
- `Hazina.AI.ContextEngineering` - Context optimization
- `Hazina.AI.Compression` - Token compression
- `Hazina.LongContext` - Long context handling

#### Reasoning & Validation
- `Hazina.Neurochain.Core` (8 deps) - Multi-layer validation
- `Hazina.AI.FaultDetection` - Hallucination detection
- `Hazina.CodeIntelligence` - Code analysis

#### Agents
- `Hazina.AI.Agents` (6 deps) - Multi-agent coordination
- `Hazina.AI.Workflows` - Workflow orchestration
- `Hazina.AI.Guardrails` - Safety constraints
- `Hazina.AgentFactory` (10 deps) - Agent creation **[REFACTOR CANDIDATE]**
- `Hazina.Generator` (5 deps) - Code generation
- `Hazina.DynamicAPI` - Dynamic API generation

#### Specialized AI
- `Hazina.AI.Vision` - Computer vision (ImageSharp, FFMpeg)
- `Hazina.AI.Training` - Fine-tuning (TorchSharp)
- `Hazina.AI.Inference` - ONNX Runtime inference
- `Hazina.AI.LocalLLM` - LLamaSharp integration
- `Hazina.AI.Memory` - Long-term memory
- `Hazina.Brain` - Integrated AI system

#### Quality & Management
- `Hazina.AI.Learning` - Feedback loops
- `Hazina.Quality` - Quality assurance
- `Hazina.Evals` - Evaluation frameworks
- `Hazina.AI.Routing` - Request routing
- `Hazina.AI.TaskPrediction` - Task prediction
- `Hazina.AI.DecisionTracking` - Decision logging
- `Hazina.AI.PromptManagement` - Prompt templates

### Layer 3: LLM Providers (8 projects)

All implement `ILLMClient` - perfect example of loose coupling:

| Provider | Models | Dependencies |
|----------|--------|--------------|
| Hazina.LLMs.OpenAI (19 deps) | GPT-4, GPT-3.5 | Client, Classes only |
| Hazina.LLMs.Anthropic (5 deps) | Claude 3 Opus/Sonnet | Client, Classes only |
| Hazina.LLMs.Gemini | Gemini Pro/Ultra | Client, Classes only |
| Hazina.LLMs.GoogleADK | Google AI SDK | Client, Classes only |
| Hazina.LLMs.Mistral | Mistral Large/Medium | Client, Classes only |
| Hazina.LLMs.HuggingFace | HF Inference API | Client, Classes only |
| Hazina.LLMs.Ollama | Local models | Client, Classes only |
| Hazina.LLMs.SemanticKernel | SK integration | Client, Classes only |

**Perfect modularity - zero cross-dependencies between providers**

### Layer 4: Tools & Services (38 projects)

#### Data Services (6 projects)
- `Hazina.Tools.Services.Database`
- `Hazina.Tools.Services.Store`
- `Hazina.Tools.Services.Embeddings`
- `Hazina.Tools.Services.DataGathering`
- `Hazina.Tools.Services.BigQuery`
- `Hazina.Tools.Services.GoogleDrive`

#### Content Services (6 projects)
- `Hazina.Tools.Services.FileOps` (10 deps)
- `Hazina.Tools.Services.ContentRetrieval`
- `Hazina.Tools.Services.Images`
- `Hazina.Tools.Services.Prompts`
- `Hazina.Tools.Services.Web`
- `Hazina.Tools.Services.WebSearch` **[MODULAR]**

#### Communication Services (3 projects)
- `Hazina.Tools.Services.Chat` (6 deps)
- `Hazina.Tools.Services.Social`
- `Hazina.Tools.Services.WordPress`

#### WebSearch Module (4 projects) - Example of good modularization
- `WebSearch.Core` - Interfaces
- `WebSearch.Infrastructure` - Base implementation
- `WebSearch.Providers` - Google, Bing, DuckDuckGo
- `WebSearch` - Unified library

#### Development Tools (3 projects)
- `Hazina.Tools.CsAutofix`
- `Hazina.Tools.UIAutomationBridge`
- `Hazina.Tools.WorkTray`

#### Production (1 project)
- `Hazina.Production.Monitoring`

### Layer 5: Infrastructure (15 projects)

- **Auth:** Hazina.Auth.Core, Hazina.Auth.Identity
- **Security:** Hazina.Security.Core, Hazina.Security.AspNetCore
- **Observability:** Hazina.Observability.Core, Hazina.Observability.AspNetCore, Hazina.Observability.LLMLogs
- **API:** Hazina.API.Generic, Hazina.Agent.API
- **Plugins:** Hazina.Core.Plugins
- **Event Sourcing:** Hazina.EventSourcing
- **Code Generation:** Hazina.CodeGeneration.Core
- **Enterprise:** Hazina.Enterprise.Core
- **Indexing:** Hazina.Indexing
- **Migration:** Hazina.Tools.Migration

### Layer 6: Applications (27 projects)

#### CLI Tools (5)
- Hazina.App.HazinaCoder (AI coding assistant)
- Hazina.App.ClaudeCode (Claude integration)
- Hazina.App.AIImage (AI image generation)
- Hazina.CLI (Framework)
- Hazina.App.HazinaCoder.Tests

#### Desktop Apps (4)
- Hazina.App.AppBuilder (Visual builder)
- Hazina.App.EmbeddingsViewer (Vector viz)
- Hazina.App.ExplorerIntegration (Windows integration)
- Hazina.App.Windows (Windows-specific)

#### Web Apps (2)
- Hazina.API.Search (Search API)
- Hazina.App.HtmlMockupGenerator (HTML gen)

#### Demos (14)
- AgenticOrchestration, Supabase, Llama, PDFMaker, LayeredImage, SmartLayeredImage, ZeroCode, GenericApi, Crosslink, FolderToPostgres, PDOK, Postgres, ConfigurationShowcase

#### Specialized Subsystems
- **TaskRunner** (3): TaskRunner, TaskRunner.UI, TaskRunner.Tests
- **Agents** (3): AgenticOrchestration, Agents.Coding, Agents.Tools
- **Legacy** (4): Hazina.Core, Hazina.Data, Services.Geometric, AI.OpenCode **[DEPRECATION CANDIDATES]**

---

## Dependency Analysis

### Key Dependency Chains

#### LLM Request Flow
```
Application
  → Hazina.AI.FluentAPI
    → Hazina.AI.Orchestration
      → Hazina.AI.Providers (failover, routing)
        → Hazina.LLMs.OpenAI / Anthropic / Ollama
          → Hazina.LLMs.Client (ILLMClient)
            → Hazina.LLMs.Classes (DTOs)
```

#### RAG Pipeline
```
Application
  → Hazina.AI.RAG
    ├→ Hazina.Store.EmbeddingStore (vector search)
    ├→ Hazina.Store.DocumentStore (retrieval)
    ├→ Hazina.Neurochain.Core (validation) [OPTIONAL?]
    └→ Hazina.AI.Providers (generation)
```

#### Agent Workflow
```
Application
  → Hazina.AI.Workflows
    → Hazina.AI.Agents
      → Hazina.AgentFactory [10 DEPENDENCIES - SPLIT?]
        ├→ Hazina.Generator
        ├→ Hazina.LLMs.Client
        └→ Hazina.Store.DocumentStore
```

### Coupling Assessment

#### ✅ Loose Coupling (Good)
- All LLM providers → ILLMClient (zero cross-deps)
- Storage abstractions → multiple implementations
- Services layer → independently deployable
- Applications → consume packages as needed

#### ⚠️ Tight Coupling (Consider Refactoring)
1. **AgentFactory** - 10 project dependencies
   - Includes: LLMs, Storage, Generator, OpenAI, SemanticKernel
   - **Proposal:** Split into Hazina.Agents.Core + Hazina.Agents.Factory

2. **RAG → Neurochain** - Mandatory dependency
   - **Proposal:** Make Neurochain optional via plugin interface

3. **FluentAPI → All Providers** - Pulls in OpenAI, Anthropic
   - **Proposal:** Create Hazina.AI.FluentAPI.Providers package

4. **Tools.Services** - Some services bundled
   - **Proposal:** Further split WebSearch, Social, Database

5. **Legacy Projects** - Hazina.Core, Hazina.Data
   - **Proposal:** Mark obsolete, migrate to new packages

---

## NuGet Package Strategy

### Current: Synchronized Versioning
- All 99 packages at v1.0.1
- **Pro:** Simple, predictable
- **Con:** Patch in one forces re-release of all

### Proposed: Layer-Based Versioning

| Layer | Version Strategy | Example |
|-------|-----------------|---------|
| Foundation | Major.Minor.Patch (stable) | 1.0.x (rare updates) |
| Core AI | Major.Minor.x (features) | 1.x.0 (monthly) |
| Providers | Independent | OpenAI 1.5.0, Anthropic 1.3.2 |
| Services | Independent | WebSearch 2.0.0, Social 1.1.0 |
| Applications | Rapid | HazinaCoder 3.2.1 (weekly) |

---

## Technology Stack

### Core
- .NET 9.0 (primary), .NET 8/10 (multi-target)
- C# 12
- ASP.NET Core 9.0
- Entity Framework Core 9.0

### AI/ML
- TorchSharp (PyTorch)
- ONNX Runtime
- LLamaSharp
- SharpToken (tiktoken)

### Storage
- SQLite
- PostgreSQL (Npgsql)
- Supabase

### External APIs
- OpenAI, Anthropic, Google AI, Mistral, HuggingFace, Ollama

### Infrastructure
- Polly (resilience)
- Microsoft.Extensions.* (DI, logging, config)
- System.Text.Json

---

## Refactoring Opportunities

### 1. AgentFactory Complexity ⭐ HIGH PRIORITY
**Current:** 10 dependencies (LLMs, Storage, Generator, Providers)
**Problem:** Too many concerns in one package
**Proposal:**
```
Hazina.Agents.Core (abstractions only)
  ├─ IAgent, IAgentFactory interfaces

Hazina.Agents.Factory (implementation)
  ├─ Depends on: Agents.Core, LLMs.Client, Storage

Hazina.Agents.Tools (tool integration)
  ├─ Depends on: Agents.Core
```
**Impact:** Better modularity, optional agent features

### 2. RAG → Neurochain Coupling ⭐ MEDIUM PRIORITY
**Current:** RAG mandatorily depends on Neurochain.Core
**Problem:** Validation should be optional
**Proposal:**
```csharp
public class RAGEngine {
    public IValidationPlugin? ValidationPlugin { get; set; }
}

// Optional plugin
Hazina.AI.RAG.Neurochain - provides NeurochainValidationPlugin
```
**Impact:** Lighter RAG for simple use cases

### 3. FluentAPI Provider Bundling ⚠️ MEDIUM PRIORITY
**Current:** FluentAPI references OpenAI, Anthropic directly
**Problem:** Pulls in all providers even if not used
**Proposal:**
```
Hazina.AI.FluentAPI (core only)
Hazina.AI.FluentAPI.OpenAI (OpenAI-specific extensions)
Hazina.AI.FluentAPI.Anthropic (Claude-specific extensions)
```
**Impact:** Smaller core, opt-in provider selection

### 4. Tools.Services Granularity ⚠️ LOW PRIORITY
**Current:** Some services bundled together
**Proposal:**
- Split WebSearch into WebSearch.Google, WebSearch.Bing
- Split Social into Social.Twitter, Social.LinkedIn
- Split Database into Database.PostgreSQL, Database.SQLite

**Impact:** More granular dependencies

### 5. Legacy Core Deprecation 🗑️ LOW PRIORITY
**Current:** Hazina.Core, Hazina.Data still exist
**Proposal:**
- Mark [Obsolete] with migration guidance
- Remove in v3.0

**Impact:** Cleaner ecosystem

---

## Testing Architecture

### Test Coverage (33 test projects)

**Unit Tests** (28 projects)
- Hazina.AI.Agents.Tests
- Hazina.AI.Providers.Tests
- Hazina.AI.Routing.Tests
- ... (25 more)

**Integration Tests** (1 project)
- Hazina.IntegrationTests.OpenAI (live API)

**Coverage Targets:**
- Foundation: 80%+
- Core AI: 70%+
- Services: 60%+ (external deps)
- Apps: E2E tests

---

## Build & Solution Structure

### Solution Files
| Solution | Projects | Use Case |
|----------|----------|----------|
| Hazina.sln | 172 | Full build, releases |
| Hazina.QuickStart.sln | ~20 | Getting started |
| Hazina.AI.sln | ~60 | AI development |
| Hazina.Core.sln | ~40 | Infrastructure |
| Hazina.Tools.sln | ~50 | Services |
| Hazina.Apps.sln | ~30 | Applications |

---

## Design Patterns

### 1. Strategy Pattern (Providers)
```csharp
ILLMClient → 8 implementations (OpenAI, Claude, Gemini, ...)
```

### 2. Decorator Pattern (Orchestration)
```csharp
LLMOrchestrator wraps ILLMClient
  → Adds: failover, cost tracking, health monitoring, circuit breaker
```

### 3. Pipeline Pattern (RAG)
```csharp
Query → Retrieval → Ranking → Context → Generation → Validation
```

### 4. Factory Pattern (Agents)
```csharp
AgentFactory.CreateAgent(config) → Instantiates + Injects + Configures
```

### 5. Repository Pattern (Storage)
```csharp
IEmbeddingStore, IDocumentStore → SQLite, PostgreSQL, Supabase
```

---

## Performance Considerations

### Vector Search
- In-Memory: Fast, limited
- SQLite: < 100K vectors
- PostgreSQL (pgvector): Millions
- Supabase: Cloud-native

### LLM Optimization
- Connection pooling
- Parallel requests
- Streaming responses
- Response caching

### Context Management
- Token compression (87% reduction possible)
- Context extension strategies
- Lossless compression

---

## Next Steps (Phase 2-5)

### Phase 2: NuGet Package Strategy (Task 869cfzy8b)
- Define package tiers (Core, Features, Integrations)
- Design versioning strategy
- Document dependency rules

### Phase 3: Consolidation (Task 869cfzy8d)
- Merge overlapping projects
- Standardize naming
- Remove deprecated packages

### Phase 4: .NET Version Standardization (Task 869cfzy8e)
- Multi-target .NET 8/9/10
- Consistent framework targets

### Phase 5: Documentation & Examples (Task 869cfzy8g)
- Per-package README files
- Usage examples
- Migration guides

---

## Summary Statistics

| Metric | Count |
|--------|-------|
| Total Projects | 172 |
| Foundation | 12 |
| Core AI | 28 |
| LLM Providers | 8 |
| Tools & Services | 38 |
| Infrastructure | 15 |
| Applications | 27 |
| Tests | 33 |
| Specialized | 11 |
| Published NuGet Packages | 99 |
| Solution Files | 6 |

---

## Document Metadata

**Created:** 2026-03-19
**Task:** 869cfzy8a - Hazina Modular Refactoring Phase 1
**Author:** Modular Architecture Audit
**Related Docs:** PACKAGE_STRATEGY.md (Phase 2), MIGRATION_GUIDE.md, PACKAGES_REGISTRY.md

---

**For contributions, see CONTRIBUTING.md**
