# Hazina Module Architecture Guide

**A developer's guide to understanding and navigating the Hazina framework's modular architecture.**

## Introduction

Hazina is a production-ready AI framework for .NET that provides multi-provider LLM orchestration, RAG (Retrieval-Augmented Generation), agent workflows, and comprehensive tooling. The framework is designed with a **layered modular architecture** that allows you to:

- Use only what you need (pick specific modules)
- Swap implementations without code changes (dependency injection friendly)
- Scale from prototype to production without rewriting
- Extend with custom implementations (interfaces over concrete types)

This guide covers the framework's architecture, core modules, dependencies, and getting started patterns.

## Architecture Overview

Hazina follows a **5-layer architecture**:

```
┌─────────────────────────────────────────────────────────────┐
│                    LAYER 5: Applications                     │
│  (Hazina.Apps.* - Console tools, web services, examples)    │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                      LAYER 4: Tools                          │
│   (Hazina.Tools.* - Domain-specific integrations: DB,        │
│    social media, files, text extraction, search, etc.)       │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                    LAYER 3: AI Capabilities                  │
│  (Hazina.AI.* - RAG, Memory, Routing, Context Engineering,  │
│   Agents, Workflows, Guardrails, Vision, LocalLLM, etc.)     │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                   LAYER 2: Storage & Agents                  │
│  (Hazina.Store.* - EmbeddingStore, DocumentStore, Facts)    │
│  (Hazina.AgentFactory, Hazina.DynamicAPI, Hazina.Generator) │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                      LAYER 1: Core LLMs                      │
│  (Hazina.LLMs.* - Multi-provider abstraction, wrappers,     │
│   OpenAI, Anthropic, Gemini, Ollama, Mistral, HuggingFace)  │
└─────────────────────────────────────────────────────────────┘
```

### Design Principles

1. **Metadata-First**: Database stores metadata (always queryable), embeddings are optional acceleration
2. **Provider-Agnostic**: Swap OpenAI ↔ Anthropic ↔ Gemini ↔ local models with configuration
3. **Production-Ready**: Built-in monitoring, cost tracking, fault detection, circuit breakers
4. **Composable**: Each module is independently useful, combine for powerful workflows
5. **Dependency Injection Friendly**: Interfaces over implementations, SOLID principles

## Core Modules Reference

### Layer 1: Core LLMs (Hazina.LLMs.*)

**Purpose**: Multi-provider LLM abstraction layer

#### Hazina.LLMs.Client
**What it does**: Defines `ILLMClient` interface - the core abstraction for all LLM interactions.

**Key types**:
- `ILLMClient` - Main interface for chat completions, embeddings, image generation, TTS
- `HazinaChatMessage` - Unified message format across all providers
- `LLMResponse<T>` - Standardized response wrapper with token usage and cost tracking

**When to use**: When you need a provider-agnostic way to interact with LLMs.

**Example**:
```csharp
using Hazina.LLMs;

ILLMClient llm = ...; // From provider wrapper or DI
var messages = new List<HazinaChatMessage>
{
    new() { Role = HazinaMessageRole.User, Text = "Hello!" }
};
var response = await llm.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, CancellationToken.None);
Console.WriteLine(response.Result); // The AI's response text
```

#### Hazina.LLMs.Registry
**What it does**: Manages multiple LLM providers with automatic failover and provider selection.

**Key types**:
- `LLMProviderRegistry` - Central registry for registered providers
- Provider configuration and automatic fallback logic

**When to use**: Production scenarios requiring high availability or multi-provider support.

#### Provider Packages (Hazina.LLMs.*)
**Available providers**:
- `Hazina.LLMs.OpenAI` - OpenAI GPT models (GPT-4, GPT-4 Turbo, GPT-3.5)
- `Hazina.LLMs.Anthropic` - Anthropic Claude models (Claude 3 Opus, Sonnet, Haiku)
- `Hazina.LLMs.Gemini` - Google Gemini models
- `Hazina.LLMs.Ollama` - Local Ollama models (Llama 2, Mistral, etc.)
- `Hazina.LLMs.Mistral` - Mistral AI models
- `Hazina.LLMs.HuggingFace` - HuggingFace Inference API
- `Hazina.LLMs.GoogleADK` - Google AI Development Kit

**Example**:
```csharp
using Hazina.LLMs.OpenAI;
using Hazina.LLMs.Anthropic;

// OpenAI
var openai = new OpenAIClientWrapper(new OpenAIConfig
{
    ApiKey = "sk-...",
    Model = "gpt-4"
});

// Anthropic
var claude = new ClaudeClientWrapper(new ClaudeConfig
{
    ApiKey = "sk-ant-...",
    Model = "claude-3-opus-20240229"
});

// Both implement ILLMClient - same interface!
```

### Layer 2: Storage (Hazina.Store.*)

**Purpose**: Persistent storage for embeddings, documents, and facts

#### Hazina.Store.EmbeddingStore
**What it does**: Stores and retrieves vector embeddings for semantic search.

**Key types**:
- `IEmbeddingStore` - Storage interface for embeddings
- `IVectorSearchStore` - Similarity search interface
- `EmbeddingService` - Orchestration of embedding generation and storage

**When to use**: RAG systems, semantic search, document similarity.

**Storage options**:
- **InMemoryVectorStore** - Development/testing (not persistent)
- **PgVectorStore** - Production PostgreSQL with pgvector extension
- **Hazina.Store.Sqlite** - SQLite for lightweight deployments

#### Hazina.Store.DocumentStore
**What it does**: Stores and retrieves full documents with metadata.

**Key features**:
- Metadata-first architecture (query without embeddings)
- Document versioning and history
- Full-text search capabilities

#### Hazina.Store.FactsStore
**What it does**: Stores structured facts and relationships for knowledge graphs.

**Use cases**: Knowledge management, entity relationship tracking, fact verification.

### Layer 2: Agents (Hazina.AgentFactory, etc.)

#### Hazina.AgentFactory
**What it does**: Factory for creating and configuring AI agents from YAML/JSON configuration.

**Key features**:
- Declarative agent configuration
- Agent lifecycle management
- Flow orchestration

**Example**:
```yaml
# agent-config.yaml
name: CustomerSupportAgent
description: Handles customer inquiries
llm:
  provider: openai
  model: gpt-4
tools:
  - database_query
  - send_email
```

#### Hazina.DynamicAPI
**What it does**: Generates REST APIs from agent configurations.

**When to use**: Expose agents as HTTP endpoints without writing controller code.

#### Hazina.Generator
**What it does**: Code generation and scaffolding for Hazina projects.

### Layer 3: AI Capabilities (Hazina.AI.*)

#### Hazina.AI.FluentAPI
**What it does**: High-level fluent API for quick setup and common patterns.

**Key types**:
- `QuickSetup` - One-line setup for OpenAI, Anthropic, etc.
- Fluent configuration builders

**Example**:
```csharp
using Hazina.AI.FluentAPI.Configuration;

var ai = QuickSetup.SetupOpenAI("sk-...");
// Ready to use! Includes sensible defaults, error handling, etc.
```

#### Hazina.AI.RAG
**What it does**: Complete RAG (Retrieval-Augmented Generation) implementation.

**Key types**:
- `RAGEngine` - Orchestrates retrieval and generation
- `Document` - Document representation with metadata
- `RAGQueryOptions` - Query configuration (TopK, similarity threshold, etc.)
- `TextChunker` - Document chunking strategies

**Example**:
```csharp
using Hazina.AI.RAG.Core;

var vectorStore = new InMemoryVectorStore();
var rag = new RAGEngine(ai, vectorStore);

await rag.IndexDocumentsAsync(documents);
var response = await rag.QueryAsync("What is Hazina?");
Console.WriteLine(response.Answer);
```

**See also**: [Basic RAG Example](examples/04-basic-rag/)

#### Hazina.AI.ContextEngineering
**What it does**: Dynamic context window management and optimization.

**Features**:
- Automatic context pruning when approaching token limits
- Priority-based context retention
- Token usage prediction

**When to use**: Long conversations, large context windows, cost optimization.

#### Hazina.AI.Memory
**What it does**: Short-term and long-term memory for agents and conversations.

**Features**:
- Sliding window memory (recent messages)
- Semantic memory (important facts)
- Episodic memory (conversation summaries)

#### Hazina.AI.Routing
**What it does**: Intelligent request routing based on task characteristics.

**Use cases**:
- Route simple queries → fast cheap model (GPT-3.5)
- Route complex queries → powerful model (GPT-4)
- Provider-based routing (cost, latency, capability)

#### Hazina.AI.Agents
**What it does**: Agent orchestration, multi-agent coordination, agent workflows.

**Patterns**:
- Single autonomous agent
- Multi-agent collaboration
- Hierarchical agent supervision

#### Hazina.AI.Guardrails
**What it does**: Safety and quality controls for AI outputs.

**Features**:
- Content filtering (PII, profanity, harmful content)
- Output validation (format, length, quality)
- Hallucination detection

#### Hazina.AI.Vision
**What it does**: Image analysis and multimodal AI capabilities.

**Features**:
- Image-to-text (describe images)
- Visual question answering
- OCR and document analysis

#### Hazina.AI.LocalLLM
**What it does**: Integration with local/on-premise LLM deployments.

**Supported**:
- Ollama
- LM Studio
- Custom GGUF models

#### Hazina.AI.Workflows
**What it does**: Declarative workflow orchestration for complex AI pipelines.

**Example workflow**:
```
User Query → Intent Classification → [
  FAQ Match → Database Lookup → Response
  Complex Query → RAG Retrieval → LLM Generation → Fact Checking → Response
]
```

#### Hazina.Neurochain.Core
**What it does**: Multi-layer reasoning with confidence scoring (inspired by neuroscience).

**When to use**: High-stakes decisions requiring verification and confidence metrics.

### Layer 4: Tools (Hazina.Tools.*)

**Purpose**: Domain-specific integrations and utilities

**Available tool categories**:

#### Database Tools
- `Hazina.Tools.Database` - SQL query execution, schema introspection
- `Hazina.Tools.Models` - Data model definitions and transformations

#### Social Media Tools
- `Hazina.Tools.LinkedIn` - LinkedIn API integration
- `Hazina.Tools.Twitter` - Twitter/X API integration

#### File & Document Tools
- `Hazina.Tools.Files` - File system operations
- `Hazina.Tools.TextExtraction` - PDF, DOCX, HTML text extraction
- `Hazina.Tools.ImageServices` - Image processing and manipulation

#### Search & Data Tools
- `Hazina.Tools.WebSearch` - Google, Bing, DuckDuckGo search integration
- `Hazina.Tools.Data` - Data transformation and validation utilities

**Example**:
```csharp
using Hazina.Tools.Database;

var dbTool = new DatabaseQueryTool(connectionString);
var result = await dbTool.ExecuteQueryAsync("SELECT * FROM customers WHERE status = 'active'");
```

### Layer 5: Applications (Hazina.Apps.*)

**Purpose**: Complete applications and example implementations

- Console applications
- Web services
- API gateways
- Example projects

## Cross-Cutting Concerns

### Observability (Hazina.Observability.*)

**What it does**: Monitoring, logging, metrics, and tracing.

**Packages**:
- `Hazina.Observability.Core` - Core abstractions
- `Hazina.Observability.AspNetCore` - ASP.NET Core integration
- `Hazina.Observability.LLMLogs` - LLM-specific logging and cost tracking

**Features**:
- Token usage tracking (per-request and aggregated)
- Cost estimation (model-specific pricing)
- Latency metrics
- Error rate tracking
- Grafana dashboard integration

### Security (Hazina.Security.*)

**What it does**: Authentication, authorization, API key management.

**Packages**:
- `Hazina.Security.Core` - Core security abstractions
- `Hazina.Security.AspNetCore` - ASP.NET Core middleware

**Features**:
- API key rotation
- Rate limiting
- PII detection and masking

### Enterprise (Hazina.Enterprise.Core)

**What it does**: Enterprise-grade features for production deployments.

**Features**:
- Multi-tenancy support
- Audit logging
- Compliance controls (GDPR, HIPAA)
- SLA monitoring

## Dependency Map

Visual dependency relationships between core modules:

```
Application Code
    │
    ├─── Hazina.AI.FluentAPI (QuickSetup)
    │        │
    │        └─── Hazina.LLMs.OpenAI
    │        └─── Hazina.LLMs.Anthropic
    │                  │
    │                  └─── Hazina.LLMs.Client (ILLMClient)
    │
    ├─── Hazina.AI.RAG
    │        │
    │        ├─── Hazina.Store.EmbeddingStore
    │        ├─── Hazina.Store.DocumentStore
    │        └─── Hazina.LLMs.Client
    │
    ├─── Hazina.AI.Agents
    │        │
    │        ├─── Hazina.AgentFactory
    │        ├─── Hazina.Tools.* (various)
    │        └─── Hazina.LLMs.Client
    │
    └─── Hazina.Observability.AspNetCore
             │
             └─── Hazina.Observability.Core
```

**Key takeaway**: Most modules depend on `Hazina.LLMs.Client` for the `ILLMClient` abstraction, making provider swapping seamless.

## Getting Started

### Installation

**Quick setup (recommended for prototypes)**:
```bash
dotnet add package Hazina.AI.FluentAPI
dotnet add package Hazina.LLMs.OpenAI
```

**Production RAG**:
```bash
dotnet add package Hazina.AI.FluentAPI
dotnet add package Hazina.LLMs.OpenAI
dotnet add package Hazina.AI.RAG
dotnet add package Hazina.Store.EmbeddingStore
```

**Multi-agent system**:
```bash
dotnet add package Hazina.AI.Agents
dotnet add package Hazina.AgentFactory
dotnet add package Hazina.Tools.Database
dotnet add package Hazina.Tools.WebSearch
```

### Configuration Patterns

#### 1. Quick Setup (Development)
```csharp
using Hazina.AI.FluentAPI.Configuration;

var ai = QuickSetup.SetupOpenAI(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!);
```

#### 2. Multi-Provider Setup (Production)
```csharp
using Hazina.LLMs.Registry;
using Hazina.LLMs.OpenAI;
using Hazina.LLMs.Anthropic;

var registry = new LLMProviderRegistry();

registry.Register("openai", new OpenAIClientWrapper(new OpenAIConfig
{
    ApiKey = config["OpenAI:ApiKey"],
    Model = "gpt-4"
}));

registry.Register("claude", new ClaudeClientWrapper(new ClaudeConfig
{
    ApiKey = config["Anthropic:ApiKey"],
    Model = "claude-3-opus-20240229"
}));

registry.SetDefaultProvider("openai");
registry.SetFallbackChain(new[] { "openai", "claude" });

// Use the registry
var ai = registry.GetProvider("openai");
```

#### 3. Dependency Injection (ASP.NET Core)
```csharp
// Startup.cs or Program.cs
services.AddSingleton<ILLMClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new OpenAIClientWrapper(new OpenAIConfig
    {
        ApiKey = config["OpenAI:ApiKey"],
        Model = "gpt-4"
    });
});

services.AddSingleton<IVectorStore, PgVectorStore>();
services.AddScoped<RAGEngine>();
```

### Environment Variables

Hazina follows .NET configuration best practices:

```bash
# LLM Providers
export OPENAI_API_KEY=sk-...
export ANTHROPIC_API_KEY=sk-ant-...
export GOOGLE_API_KEY=...

# Database (for persistent storage)
export DATABASE_URL=postgresql://user:password@localhost:5432/hazina

# Optional: Observability
export GRAFANA_API_KEY=...
export ENABLE_TELEMETRY=true
```

## Migration from Pre-Modular Architecture

If upgrading from Hazina v1.x (pre-modular architecture):

### 1. Update Package References
**Before**:
```xml
<PackageReference Include="Hazina" Version="1.0.0" />
```

**After**:
```xml
<PackageReference Include="Hazina.AI.FluentAPI" Version="1.0.1" />
<PackageReference Include="Hazina.LLMs.OpenAI" Version="1.0.1" />
```

### 2. Update Using Statements
**Before**:
```csharp
using Hazina;
using Hazina.LLMs;
```

**After**:
```csharp
using Hazina.LLMs;
using Hazina.LLMs.OpenAI;
using Hazina.AI.FluentAPI.Configuration;
```

### 3. Update Configuration Patterns
**Before** (constructor parameters):
```csharp
var config = new OpenAIConfig("sk-...", "gpt-4");
```

**After** (object initializers):
```csharp
var config = new OpenAIConfig
{
    ApiKey = "sk-...",
    Model = "gpt-4"
};
```

### 4. Update Method Calls
**Before**:
```csharp
var response = await llm.GenerateTextAsync(prompt, options);
```

**After**:
```csharp
var messages = new List<HazinaChatMessage>
{
    new() { Role = HazinaMessageRole.User, Text = prompt }
};
var response = await llm.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, CancellationToken.None);
```

See the full [Migration Guide](MIGRATION_GUIDE.md) for complete migration paths.

## Common Scenarios

### Scenario 1: Simple Question Answering
**Modules needed**: `Hazina.AI.FluentAPI`, `Hazina.LLMs.OpenAI`

**See**: [Hello World Example](examples/01-hello-world/)

### Scenario 2: Document Q&A (RAG)
**Modules needed**: `Hazina.AI.RAG`, `Hazina.Store.EmbeddingStore`, `Hazina.LLMs.OpenAI`

**See**: [Basic RAG Example](examples/04-basic-rag/)

### Scenario 3: Multi-Agent Workflow
**Modules needed**: `Hazina.AI.Agents`, `Hazina.AgentFactory`, `Hazina.Tools.*`, `Hazina.LLMs.OpenAI`

**See**: [Multi-Agent Example](examples/10-multi-agent/)

### Scenario 4: Production System with Monitoring
**Modules needed**: `Hazina.AI.RAG`, `Hazina.Store.EmbeddingStore`, `Hazina.Observability.AspNetCore`, `Hazina.Security.AspNetCore`, `Hazina.LLMs.OpenAI`

**See**: [Production RAG Example](examples/05-production-rag/)

## Performance Considerations

### Caching
- **Embedding cache**: Reuse embeddings for identical text (automatic in `EmbeddingService`)
- **Response cache**: Cache LLM responses for deterministic queries
- **Metadata cache**: Index metadata in database for fast filtering

### Batching
- **Embedding generation**: Batch document indexing (RAGEngine supports batch operations)
- **Database queries**: Use batch inserts for large document sets

### Provider Selection
- **Development**: OpenAI GPT-3.5 (fast, cheap)
- **Production**: OpenAI GPT-4 or Claude 3 Opus (high quality)
- **High volume**: Local Ollama models (no API costs)
- **Cost optimization**: Use routing to match task complexity to model capability

## Troubleshooting

### Common Issues

#### "Provider not found" Error
**Problem**: LLM provider not registered or package not installed.

**Solution**:
```bash
dotnet add package Hazina.LLMs.OpenAI  # Install provider package
```

#### "Embedding dimension mismatch" Error
**Problem**: Vector store configured for different embedding dimension than LLM provides.

**Solution**: Ensure vector store dimension matches your embedding model:
- OpenAI text-embedding-ada-002: 1536 dimensions
- OpenAI text-embedding-3-small: 1536 dimensions
- OpenAI text-embedding-3-large: 3072 dimensions

#### High API Costs
**Problem**: Too many embedding generation requests.

**Solution**:
- Use persistent vector store (PostgreSQL) to avoid re-indexing
- Enable embedding caching in `EmbeddingService`
- Use metadata-first search when possible (no embeddings needed)

#### Slow RAG Queries
**Problem**: Large document sets or inefficient retrieval.

**Solution**:
- Add metadata filters to reduce search space
- Optimize TopK parameter (default 5 is usually good)
- Use PgVector indexes for faster similarity search
- Consider chunking strategy (smaller chunks = more precise retrieval)

## Additional Resources

### Documentation
- [Getting Started Guide](GETTING_STARTED.md)
- [API Documentation](apidoc/api/index.html)
- [Code Examples](examples/)
- [Advanced Scenarios](ADVANCED_SCENARIOS.md)
- [RAG Guide](RAG_GUIDE.md)
- [Agents Guide](AGENTS_GUIDE.md)

### Package Registry
- [NuGet Packages](https://www.nuget.org/packages?q=owner:martiendejong+Hazina)
- [Packages Registry](../PACKAGES_REGISTRY.md)

### Community
- [GitHub Repository](https://github.com/martiendejong/Hazina)
- [GitHub Discussions](https://github.com/martiendejong/Hazina/discussions)
- [Issue Tracker](https://github.com/martiendejong/Hazina/issues)

## Summary

Hazina's modular architecture provides:

✅ **Flexibility**: Use only what you need
✅ **Scalability**: Start simple, add complexity as needed
✅ **Maintainability**: Clear separation of concerns
✅ **Testability**: Mock interfaces for unit testing
✅ **Production-Ready**: Built-in monitoring, fault tolerance, security

**Key architecture principles**:
1. **Layer 1 (LLMs)**: Provider abstraction via `ILLMClient`
2. **Layer 2 (Storage/Agents)**: Persistent data and agent orchestration
3. **Layer 3 (AI)**: High-level capabilities (RAG, Memory, Routing, etc.)
4. **Layer 4 (Tools)**: Domain-specific integrations
5. **Layer 5 (Apps)**: Complete applications

**Next steps**:
- Browse [Code Examples](examples/) for hands-on learning
- Read [Getting Started Guide](GETTING_STARTED.md) for detailed tutorials
- Explore [API Documentation](apidoc/api/index.html) for reference

---

**Happy building with Hazina!**
