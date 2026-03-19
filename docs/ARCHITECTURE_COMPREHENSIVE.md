# Hazina Framework Architecture

**Version:** 2.0
**Last Updated:** 2026-03-19
**Total Projects:** 172
**Published NuGet Packages:** 99

## Executive Summary

Hazina is a comprehensive .NET AI infrastructure framework designed to scale from prototype to production without code rewrites. The framework consists of 172 C# projects organized into a layered architecture with clear separation of concerns across Core AI capabilities, Tools & Services, Infrastructure, and Applications.

### Key Architectural Principles

1. **Modular Design** - Each package is independently versioned and deployable
2. **Loose Coupling** - Core abstractions enable provider-agnostic implementations
3. **Layered Architecture** - Foundation → Core → Services → Applications
4. **Multi-Provider Support** - Unified API across OpenAI, Anthropic, Ollama, and local models
5. **Production-Ready** - Built-in monitoring, fault detection, and cost tracking

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           APPLICATIONS LAYER                                 │
│  CLI Tools (5) | Desktop Apps (4) | Web Apps (2) | Demos (14)               │
└─────────────────────────────────────────────────────────────────────────────┘
                                     ▲
                                     │
┌─────────────────────────────────────────────────────────────────────────────┐
│                          TOOLS & SERVICES LAYER                              │
│  Services (23) | Common (2) | Development (3) | Production (1)               │
└─────────────────────────────────────────────────────────────────────────────┘
                                     ▲
                                     │
┌─────────────────────────────────────────────────────────────────────────────┐
│                              CORE AI LAYER                                   │
│  AI Core (28) | LLM Providers (8) | Agents (3) | Storage (4)                │
└─────────────────────────────────────────────────────────────────────────────┘
                                     ▲
                                     │
┌─────────────────────────────────────────────────────────────────────────────┐
│                          FOUNDATION LAYER                                    │
│  LLMs.Client (49 deps) | LLMs.Classes (36 deps) | Tools.Data (26 deps)      │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Layer 1: Foundation (Core Abstractions)

### Most Critical Projects (by dependency count)

| Project | Dependents | Purpose |
|---------|-----------|---------|
| **Hazina.LLMs.Client** | 49 | Core LLM client abstraction and interfaces |
| **Hazina.LLMs.Classes** | 36 | Shared data models and DTOs |
| **Hazina.Tools.Data** | 26 | Data access abstractions |
| **Hazina.Tools.Models** | 25 | Common domain models |
| **Hazina.LLMs.Helpers** | 20 | Utility functions for LLM operations |
| **Hazina.Tools.Core** | 19 | Core tool infrastructure |
| **Hazina.AI.Providers** | 19 | Provider abstraction layer |
| **Hazina.Store.EmbeddingStore** | 19 | Vector embeddings storage |

### Key Characteristics

- **Zero external dependencies** on other Hazina projects (leaf nodes)
- Defines all core interfaces and contracts
- Stable APIs (breaking changes require major version bump)
- Used by 90%+ of the codebase

### Foundation Projects Detail

#### LLM Core (6 projects)
- `Hazina.LLMs.Client` - ILLMClient interface, request/response handling
- `Hazina.LLMs.Classes` - ChatMessage, ChatResponse, ModelConfig DTOs
- `Hazina.LLMs.Helpers` - Token counting, message formatting
- `Hazina.LLMs.Registry` - Model capability registry
- `Hazina.LLMs.Tools` - Tool/function calling interfaces
- `Hazina.LLMClientTools` - Tool implementation helpers

#### Storage Foundation (4 projects)
- `Hazina.Store.EmbeddingStore` - Vector database abstraction
- `Hazina.Store.DocumentStore` - Document storage interfaces
- `Hazina.Store.FactsStore` - Knowledge facts storage
- `Hazina.Store.Sqlite` - SQLite implementation

#### Tools Foundation (7 projects)
- `Hazina.Tools.Core` - Base tool infrastructure
- `Hazina.Tools.Data` - Database access patterns
- `Hazina.Tools.Models` - Domain models
- `Hazina.Tools.Extensions` - Extension methods
- `Hazina.Tools.TextExtraction` - Text parsing utilities
- `Hazina.Tools.ContextCompression` - Context window management
- `Hazina.Tools.AI.Agents` - Agent infrastructure

---

## Layer 2: Core AI Capabilities

### 2.1 AI Core (28 projects)

#### Core AI Infrastructure
- `Hazina.AI.Core` - Base AI abstractions (9 dependents)
- `Hazina.AI.Providers` - Multi-provider orchestration (19 dependents)
- `Hazina.AI.Orchestration` - Request routing and failover
- `Hazina.AI.FluentAPI` - Fluent configuration API

#### RAG & Context Engineering
- `Hazina.AI.RAG` - Retrieval-Augmented Generation (9 dependents)
- `Hazina.AI.ContextEngineering` - Context optimization
- `Hazina.AI.Compression` - Token compression
- `Hazina.LongContext` - Long context handling

#### Multi-Layer Reasoning
- `Hazina.Neurochain.Core` - Multi-layer validation (8 dependents)
- `Hazina.AI.FaultDetection` - Hallucination detection
- `Hazina.CodeIntelligence` - Code analysis AI

#### Agents & Workflows
- `Hazina.AI.Agents` - Multi-agent coordination (6 dependents)
- `Hazina.AI.Workflows` - Workflow orchestration
- `Hazina.AI.Guardrails` - Safety constraints
- `Hazina.AgentFactory` - Agent instantiation (10 dependents)

#### Specialized AI
- `Hazina.AI.Vision` - Computer vision
- `Hazina.AI.Training` - Model fine-tuning (TorchSharp)
- `Hazina.AI.Inference` - ONNX Runtime inference
- `Hazina.AI.LocalLLM` - LLamaSharp integration
- `Hazina.AI.Memory` - Long-term memory
- `Hazina.Brain` - Integrated AI brain

#### Quality & Monitoring
- `Hazina.AI.Learning` - Feedback loops
- `Hazina.Quality` - Quality assurance
- `Hazina.Evals` - Evaluation frameworks
- `Hazina.AI.Routing` - Intelligent routing
- `Hazina.AI.TaskPrediction` - Task prediction
- `Hazina.AI.DecisionTracking` - Decision logging
- `Hazina.AI.PromptManagement` - Prompt templates

### 2.2 LLM Providers (8 projects)

All providers implement `ILLMClient` for seamless swapping:

| Provider | Project | Models |
|----------|---------|--------|
| OpenAI | `Hazina.LLMs.OpenAI` | GPT-4, GPT-4 Turbo, GPT-3.5 |
| Anthropic | `Hazina.LLMs.Anthropic` | Claude 3 Opus, Sonnet, Haiku |
| Google | `Hazina.LLMs.Gemini` | Gemini Pro, Ultra |
| Google ADK | `Hazina.LLMs.GoogleADK` | Google AI SDK |
| Mistral | `Hazina.LLMs.Mistral` | Mistral Large, Medium |
| HuggingFace | `Hazina.LLMs.HuggingFace` | Inference API models |
| Ollama | `Hazina.LLMs.Ollama` | Local models (Llama, Mistral, etc) |
| Semantic Kernel | `Hazina.LLMs.SemanticKernel` | SK integration |

**Dependencies:** All depend on `Hazina.LLMs.Client` and `Hazina.LLMs.Classes` only.

### 2.3 Agents (3 projects)

- `Hazina.AgentFactory` - Agent creation and lifecycle (10 dependents)
  - Dependencies: LLMs, Storage, Generator
- `Hazina.Generator` - Code/content generation (5 dependents)
- `Hazina.DynamicAPI` - Dynamic API generation

---

## Layer 3: Tools & Services

### 3.1 Services (23 projects)

#### Data & Storage Services
- `Hazina.Tools.Services.Database` - Database operations
- `Hazina.Tools.Services.Store` - Store management
- `Hazina.Tools.Services.Embeddings` - Embedding generation
- `Hazina.Tools.Services.DataGathering` - Data collection
- `Hazina.Tools.Services.BigQuery` - Google BigQuery
- `Hazina.Tools.Services.GoogleDrive` - Drive integration

#### Content Services
- `Hazina.Tools.Services.FileOps` - File operations (10 dependents)
- `Hazina.Tools.Services.ContentRetrieval` - Content fetching
- `Hazina.Tools.Services.Images` - Image processing
- `Hazina.Tools.Services.Prompts` - Prompt management
- `Hazina.Tools.Services.Web` - Web scraping
- `Hazina.Tools.Services.WebSearch` - Multi-engine search

#### Communication Services
- `Hazina.Tools.Services.Chat` - Chat interfaces (6 dependents)
- `Hazina.Tools.Services.Social` - Social media APIs
- `Hazina.Tools.Services.WordPress` - WordPress integration

#### Specialized Services
- `Hazina.Tools.Services.ToolAgent` - Tool agent execution
- `Hazina.Tools.Services.Intake` - Data intake
- `Hazina.Tools.Services.PDOK` - Dutch geo data
- `Hazina.Tools.Services` - Base service infrastructure

#### WebSearch Module (4 projects)
- `WebSearch.Core` - Core search interfaces
- `WebSearch.Infrastructure` - Search infrastructure
- `WebSearch.Providers` - Search engine providers
- `WebSearch` - Unified search library

### 3.2 Development Tools (3 projects)

- `Hazina.Tools.CsAutofix` - C# code fixing
- `Hazina.Tools.UIAutomationBridge` - UI automation
- `Hazina.Tools.WorkTray` - Development utilities

### 3.3 Production Tools (1 project)

- `Hazina.Production.Monitoring` - Metrics, health checks, cost tracking

---

## Layer 4: Infrastructure

### 4.1 Authentication (2 projects)
- `Hazina.Auth.Core` - Auth abstractions
- `Hazina.Auth.Identity` - Identity implementation

### 4.2 Observability (3 projects)
- `Hazina.Observability.Core` - Logging/metrics abstractions
- `Hazina.Observability.AspNetCore` - ASP.NET Core integration
- `Hazina.Observability.LLMLogs` - LLM-specific logging

### 4.3 Security (2 projects)
- `Hazina.Security.Core` - Security primitives
- `Hazina.Security.AspNetCore` - Web security

### 4.4 API & Integration (3 projects)
- `Hazina.API.Generic` - Generic CRUD APIs
- `Hazina.Agent.API` - Agent HTTP API
- `Hazina.Core.Plugins` - Plugin system

### 4.5 Other Infrastructure (5 projects)
- `Hazina.EventSourcing` - Event sourcing
- `Hazina.CodeGeneration.Core` - Code generation
- `Hazina.Enterprise.Core` - Enterprise features
- `Hazina.Indexing` - Search indexing
- `Hazina.Tools.Migration` - Data migration

---

## Layer 5: Applications

### 5.1 CLI Tools (5 projects)
- `Hazina.App.HazinaCoder` - AI coding assistant
- `Hazina.App.ClaudeCode` - Claude integration
- `Hazina.App.AIImage` - AI image generation
- `Hazina.CLI` - General CLI framework
- `Hazina.App.HazinaCoder.Tests` - CLI tests

### 5.2 Desktop Applications (4 projects)
- `Hazina.App.AppBuilder` - Visual app builder
- `Hazina.App.EmbeddingsViewer` - Vector visualization
- `Hazina.App.ExplorerIntegration` - Windows Explorer integration
- `Hazina.App.Windows` - Windows-specific features

### 5.3 Web Applications (2 projects)
- `Hazina.API.Search` - Search API service
- `Hazina.App.HtmlMockupGenerator` - HTML generation tool

### 5.4 Demo Applications (14 projects)
- `Hazina.Demo.AgenticOrchestration` - Multi-agent demo
- `Hazina.Demo.Supabase` - Supabase integration
- `Hazina.Demo.Llama` - Local LLM demo
- `Hazina.Demo.PDFMaker` - PDF generation
- `Hazina.Demo.LayeredImage` - Image composition
- `Hazina.Demo.SmartLayeredImage` - AI image layering
- `Hazina.Demo.ZeroCode` - No-code AI
- `Hazina.Demo.GenericApi` - API generation
- `Hazina.Demo.Crosslink` - Cross-project linking
- `Hazina.Demo.FolderToPostgres` - Data import
- `Hazina.Demo.PDOK` - Dutch geo demo
- `Hazina.Demo.Postgres` - PostgreSQL demo
- `Hazina.Demo.ConfigurationShowcase` - Config examples

---

## Specialized Subsystems

### TaskRunner Subsystem (3 projects)
- `Hazina.TaskRunner` - Task execution engine
- `Hazina.TaskRunner.UI` - Task UI
- `Hazina.TaskRunner.Tests` - Task tests

### Agentic Orchestration (3 projects)
- `Hazina.AgenticOrchestration` - Multi-agent orchestration
- `Hazina.Agents.Coding` - Coding agents
- `Hazina.Agents.Tools` - Agent tools

### Legacy Core (4 projects)
- `Hazina.Core` - Original monolithic core
- `Hazina.Data` - Original data layer
- `Hazina.Services.Geometric` - Geometric services
- `Hazina.AI.OpenCode` - OpenCode integration

---

## Dependency Analysis

### Core Dependency Chains

#### Chain 1: LLM Request Flow
```
Application
  ↓
Hazina.AI.FluentAPI
  ↓
Hazina.AI.Orchestration
  ↓
Hazina.AI.Providers (Multi-provider failover)
  ↓
Hazina.LLMs.OpenAI / Anthropic / Ollama
  ↓
Hazina.LLMs.Client (ILLMClient interface)
  ↓
Hazina.LLMs.Classes (DTOs)
```

#### Chain 2: RAG Pipeline
```
Application
  ↓
Hazina.AI.RAG (RAGEngine)
  ↓
├─ Hazina.Store.EmbeddingStore (Vector search)
├─ Hazina.Store.DocumentStore (Document retrieval)
└─ Hazina.AI.Providers (LLM generation)
    ↓
    Hazina.LLMs.Client
```

#### Chain 3: Agent Workflow
```
Application
  ↓
Hazina.AI.Workflows
  ↓
Hazina.AI.Agents
  ↓
Hazina.AgentFactory
  ↓
├─ Hazina.Generator (Tool generation)
├─ Hazina.LLMs.Client (LLM calls)
└─ Hazina.Store.DocumentStore (State persistence)
```

### Coupling Analysis

#### Tightly Coupled (High cohesion, appropriate)
- `Hazina.LLMs.Client` ↔ `Hazina.LLMs.Classes` (Core DTOs)
- `Hazina.AI.Providers` ↔ LLM Providers (Strategy pattern)
- `Hazina.AI.Orchestration` ↔ `Hazina.AI.Providers` (Orchestration layer)

#### Loosely Coupled (Good modularity)
- All LLM Providers implement `ILLMClient` - zero cross-dependencies
- Storage providers implement common interfaces
- Tools/Services are independently deployable

#### Potential Concerns
1. **AgentFactory has 10 dependencies** - Consider splitting into smaller packages
2. **AI.RAG depends on Neurochain.Core** - Validation should be optional
3. **FluentAPI pulls in multiple providers** - Should be opt-in via separate packages

---

## NuGet Package Organization

### Current State (99 packages published)

#### Package Categories
- **🤖 Core AI & LLM Providers** (38 packages) - Core orchestration, all providers
- **🛠️ Tools & Services** (33 packages) - Database, web, file operations
- **🔐 Storage, Security & Observability** (13 packages) - Infrastructure
- **🎯 Agents, CodeGen, API & UI** (15 packages) - High-level features

### Package Publishing Pattern
- **Version:** All packages at v1.0.1 (synchronized versioning)
- **Owner:** martiendejong on NuGet.org
- **Dependencies:** Explicit version pinning within Hazina packages
- **Target Framework:** .NET 9.0 (some multi-target .NET 8/9/10)

### NuGet Dependency Strategy

#### Current: Synchronized Versioning
- **Pro:** Simple to understand, all packages move together
- **Con:** Patch in one package forces re-release of all 99 packages

#### Proposed: Semantic Versioning by Layer
- **Foundation (Layer 1):** Rarely changes, long-term stability
- **Core AI (Layer 2):** Minor version bumps for new features
- **Services (Layer 3):** Independent versioning per service
- **Applications (Layer 5):** Rapid iteration, frequent patches

---

## Technology Stack

### Core Technologies
- **.NET 9.0** - Primary target framework
- **C# 12** - Latest language features
- **ASP.NET Core** - Web applications
- **Entity Framework Core 9.0** - Database access

### AI/ML Libraries
- **TorchSharp** - PyTorch bindings for .NET
- **ONNX Runtime** - Cross-platform inference
- **LLamaSharp** - Local LLM inference
- **SharpToken** - Token counting (tiktoken port)

### Storage
- **SQLite** - Embedded database
- **PostgreSQL** (Npgsql) - Primary database
- **Supabase** - Cloud backend

### External Services
- **OpenAI API** - GPT models
- **Anthropic API** - Claude models
- **Google AI APIs** - Gemini models
- **Ollama** - Local model hosting

### Infrastructure
- **Polly** - Resilience and fault handling
- **Microsoft.Extensions.*** - Dependency injection, logging, configuration
- **System.Text.Json** - JSON serialization

---

## Design Patterns

### 1. Strategy Pattern (Provider Abstraction)
```csharp
public interface ILLMClient
{
    Task<ChatResponse> GetResponseAsync(List<ChatMessage> messages);
}

// 8 implementations: OpenAI, Anthropic, Gemini, Mistral, Ollama, etc.
```

### 2. Decorator Pattern (Orchestration)
```csharp
LLMOrchestrator wraps ILLMClient
  → Adds failover
  → Adds cost tracking
  → Adds health monitoring
  → Adds circuit breaker
```

### 3. Pipeline Pattern (RAG)
```csharp
Query → Retrieval → Ranking → Context Assembly → Generation → Validation
```

### 4. Factory Pattern (Agents)
```csharp
AgentFactory.CreateAgent(config)
  → Instantiates agent
  → Injects dependencies
  → Configures tools
```

### 5. Repository Pattern (Storage)
```csharp
IEmbeddingStore, IDocumentStore, IFactsStore
  → SQLite, PostgreSQL, Supabase implementations
```

---

## Configuration Architecture

### 1. Provider Configuration
```csharp
{
  "Hazina": {
    "Providers": [
      {
        "Name": "OpenAI",
        "Type": "OpenAI",
        "ApiKey": "...",
        "Priority": 1,
        "MaxConcurrentRequests": 10
      },
      {
        "Name": "Claude",
        "Type": "Anthropic",
        "ApiKey": "...",
        "Priority": 2
      }
    ]
  }
}
```

### 2. RAG Configuration
```csharp
{
  "RAG": {
    "ChunkSize": 1000,
    "ChunkOverlap": 200,
    "TopK": 5,
    "MinSimilarity": 0.7,
    "EnableReranking": true
  }
}
```

### 3. Agent Configuration
```csharp
{
  "Agents": {
    "Researcher": {
      "Tools": ["WebSearch", "DocumentRetrieval"],
      "MaxIterations": 5,
      "Temperature": 0.7
    }
  }
}
```

---

## Testing Architecture

### Test Projects (33 total)

#### Unit Tests
- `Hazina.AI.Agents.Tests`
- `Hazina.AI.Providers.Tests`
- `Hazina.AI.Routing.Tests`
- `Hazina.AI.Workflows.Tests`
- `Hazina.TaskRunner.Tests`
- ... (28 more test projects)

#### Integration Tests
- `Hazina.IntegrationTests.OpenAI` - Live API tests

#### Test Coverage Strategy
- **Foundation Layer:** 80%+ coverage required
- **Core AI Layer:** 70%+ coverage required
- **Services Layer:** 60%+ coverage (external dependencies)
- **Applications:** End-to-end tests

---

## Build & Solution Structure

### Solution Files

#### Hazina.sln (Master)
- All 172 projects
- Use for: Full framework builds, releases

#### Hazina.QuickStart.sln
- Minimal set for getting started
- Core AI + OpenAI provider + RAG

#### Hazina.AI.sln
- All AI-related projects
- Use for: AI feature development

#### Hazina.Core.sln
- Foundation + Core AI
- Use for: Core infrastructure work

#### Hazina.Tools.sln
- All Tools & Services
- Use for: Service development

#### Hazina.Apps.sln
- All application projects
- Use for: Application development

### Build Targets
- **Debug:** Local development with logging
- **Release:** Optimized for production
- **Publish:** NuGet package generation

---

## Deployment Architecture

### Package Deployment
1. **NuGet.org** - Public packages (99 packages)
2. **Private feeds** - Internal/beta packages
3. **MSI Installer** - Hazina Orchestration Service (desktop)

### Application Deployment
1. **CLI Tools** - Standalone executables
2. **Desktop Apps** - ClickOnce or MSI
3. **Web Services** - Docker containers
4. **Demos** - Local/cloud hosting

---

## Security Architecture

### API Key Management
- Environment variables (recommended)
- Configuration files (encrypted)
- Azure Key Vault integration
- User secrets (development)

### Data Protection
- Hazina.Security.Core - Encryption primitives
- Hazina.Security.AspNetCore - Web security
- Hazina.Auth.* - Authentication/authorization

### Observability
- Hazina.Observability.Core - Structured logging
- Hazina.Observability.LLMLogs - LLM-specific telemetry
- Hazina.Production.Monitoring - Metrics & health checks

---

## Performance Considerations

### Vector Search Optimization
- **In-Memory Store:** Fast, limited capacity
- **SQLite Store:** Good for < 100K vectors
- **PostgreSQL (pgvector):** Production scale (millions)
- **Supabase:** Cloud-native vector search

### LLM Request Optimization
- **Connection pooling** - Reuse HTTP clients
- **Parallel requests** - Batch processing
- **Streaming responses** - Token-by-token for UX
- **Caching** - Response caching for identical queries

### Context Window Management
- `Hazina.AI.Compression` - Intelligent token reduction
- `Hazina.LongContext` - Context extension strategies
- `Hazina.Tools.ContextCompression` - Lossless compression

---

## Migration & Breaking Changes

### v1.0 → v2.0 Breaking Changes
1. **Config classes** - Constructor → Object initializers
2. **Namespaces** - Provider-specific namespaces added
3. **Method signatures** - `GenerateTextAsync` → `GenerateAsync`

See `docs/MIGRATION_GUIDE.md` for complete migration paths.

---

## Refactoring Opportunities

### Identified Issues

1. **AgentFactory Complexity**
   - **Current:** 10 project dependencies
   - **Proposal:** Split into Hazina.Agents.Core (abstractions) + Hazina.Agents.Factory (implementation)
   - **Impact:** Better modularity, optional agent features

2. **Neurochain as Mandatory RAG Dependency**
   - **Current:** Hazina.AI.RAG → Hazina.Neurochain.Core
   - **Proposal:** Make Neurochain optional via plugin interface
   - **Impact:** Lighter RAG package for simple use cases

3. **FluentAPI Provider Coupling**
   - **Current:** FluentAPI references all providers (OpenAI, Anthropic, etc.)
   - **Proposal:** Create Hazina.AI.FluentAPI.Providers as separate package
   - **Impact:** Smaller core package, opt-in provider selection

4. **Tools.Services Granularity**
   - **Current:** Some services are tightly bundled
   - **Proposal:** Further split WebSearch, Social, Database services
   - **Impact:** More granular dependencies, smaller package sizes

5. **Legacy Core Projects**
   - **Current:** Hazina.Core, Hazina.Data still exist from v1
   - **Proposal:** Mark as obsolete, migrate users to new packages
   - **Impact:** Cleaner package ecosystem

---

## Future Architecture Direction

### Phase 3: Consolidation (Planned 2026 Q2)
1. Merge overlapping projects
2. Standardize naming conventions
3. Remove deprecated packages
4. Update all dependencies

### Phase 4: .NET Version Standardization (Planned 2026 Q2)
1. Multi-target .NET 8/9/10 for all packages
2. Consistent framework targets across layers
3. Remove .NET 9-only constraints where possible

### Phase 5: Documentation & Examples (This Phase!)
1. Per-package README files
2. Usage examples for each package
3. Migration guides
4. Architecture diagrams

### Future Phases
- **Phase 6:** Performance optimization
- **Phase 7:** Cloud-native features
- **Phase 8:** Enterprise features

---

## Appendix: Complete Project List

### Foundation (12 projects)
1. Hazina.LLMs.Client
2. Hazina.LLMs.Classes
3. Hazina.Tools.Data
4. Hazina.Tools.Models
5. Hazina.LLMs.Helpers
6. Hazina.Tools.Core
7. Hazina.Store.EmbeddingStore
8. Hazina.Store.DocumentStore
9. Hazina.Store.FactsStore
10. Hazina.Store.Sqlite
11. Hazina.LLMs.Tools
12. Hazina.LLMClientTools

### Core AI (28 projects)
Listed in "Layer 2: Core AI Capabilities" section above.

### LLM Providers (8 projects)
1. Hazina.LLMs.OpenAI
2. Hazina.LLMs.Anthropic
3. Hazina.LLMs.Gemini
4. Hazina.LLMs.GoogleADK
5. Hazina.LLMs.Mistral
6. Hazina.LLMs.HuggingFace
7. Hazina.LLMs.Ollama
8. Hazina.LLMs.SemanticKernel

### Tools & Services (38 projects)
Listed in "Layer 3: Tools & Services" section above.

### Infrastructure (15 projects)
Listed in "Layer 4: Infrastructure" section above.

### Applications (27 projects)
Listed in "Layer 5: Applications" section above.

### Tests (33 projects)
All test projects for unit and integration testing.

### Specialized (11 projects)
TaskRunner subsystem, Agentic Orchestration, Legacy Core.

---

## Document Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-03-19 | Modular Refactoring Phase 1 | Initial comprehensive architecture audit |

---

**Related Documents:**
- `PACKAGE_STRATEGY.md` - NuGet package organization strategy (Phase 2)
- `MIGRATION_GUIDE.md` - v1 → v2 migration guide
- `SOLUTIONS.md` - Solution file selection guide
- `PACKAGES_REGISTRY.md` - Complete package catalog
- `SERVICES_REGISTRY.md` - Complete service catalog

**For questions or contributions, see CONTRIBUTING.md**
