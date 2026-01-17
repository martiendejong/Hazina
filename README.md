# Hazina

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/hazina-ai/hazina)
[![NuGet](https://img.shields.io/badge/NuGet-coming%20soon-blue)](https://www.nuget.org/)
[![Documentation](https://img.shields.io/badge/docs-docs/apidoc-blue)](docs/apidoc/index.html)
[![Deploy Docs](https://github.com/martiendejong/Hazina/actions/workflows/deploy-docs.yml/badge.svg)](https://github.com/martiendejong/Hazina/actions/workflows/deploy-docs.yml)

**Production-ready AI infrastructure for .NET that scales from prototype to production without rewriting your code.**

## ⚠️ Breaking Changes in v2.0

If upgrading from v1.x, please note these important changes:

- **Config classes:** Use object initializers instead of constructor parameters ([Migration Guide](docs/MIGRATION_GUIDE.md))
- **Namespaces:** Add `using Hazina.LLMs.OpenAI;` for OpenAI-specific classes
- **Method signatures:** `GenerateTextAsync` → `GenerateAsync` with updated parameters

See the full [API Changelog](docs/API_CHANGELOG.md) for details and migration paths.

---

## Why Hazina Instead of X?

| | **Hazina** | **LangChain** | **Semantic Kernel** | **Roll Your Own** |
|---|---|---|---|---|
| **Language** | C# native | Python-first | C# | C# |
| **Setup time** | 4 lines | 50+ lines | 30+ lines | 200+ lines |
| **Multi-provider failover** | Built-in | Manual | Plugin required | Build yourself |
| **Hallucination detection** | Built-in | External tools | Not included | Build yourself |
| **Cost tracking** | Automatic | Manual | Manual | Build yourself |
| **Production monitoring** | Included | External | External | Build yourself |
| **Local + Cloud** | Unified API | Separate configs | Separate configs | Multiple implementations |

### Hazina wins because:

- **4 lines to production** — One-line setup, automatic provider failover, built-in fault detection
- **No vendor lock-in** — Switch between OpenAI, Anthropic, local models with zero code changes
- **Ship faster** — RAG, agents, embeddings, and monitoring included — not bolted on

## 30-Minute Quickstart

Build a production-ready RAG AI that answers questions from your documents:

```bash
dotnet new console -n MyRAGApp
cd MyRAGApp
dotnet add package Hazina.AI.FluentAPI
dotnet add package Hazina.AI.RAG
```

```csharp
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.RAG.Core;

// 1. Setup (one line)
var ai = QuickSetup.SetupOpenAI(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!);

// 2. Create RAG engine
var vectorStore = new InMemoryVectorStore();
var rag = new RAGEngine(ai, vectorStore);

// 3. Index your documents
await rag.IndexDocumentsAsync(new List<Document>
{
    new() { Content = "Hazina is a .NET AI framework for production applications." },
    new() { Content = "RAG combines retrieval with generation for accurate answers." }
});

// 4. Query with context
var response = await rag.QueryAsync("What is Hazina?");
Console.WriteLine(response.Answer);
```

**This scales from demo → production without rewriting.**

See the full [30-Minute RAG Tutorial](docs/quickstart.md) for:
- Swap LLM providers via config
- Add PostgreSQL/Supabase backend
- Enable/disable embeddings
- Add multi-layer reasoning

## Installation

```bash
# Core package (minimal)
dotnet add package Hazina.AI.FluentAPI

# Add RAG capabilities
dotnet add package Hazina.AI.RAG

# Add agentic workflows
dotnet add package Hazina.AI.Agents

# Add production monitoring
dotnet add package Hazina.Production.Monitoring
```

## Feature Comparison

### vs LangChain (Python)

```python
# LangChain - 15+ lines, Python only
from langchain.llms import OpenAI
from langchain.chains import RetrievalQA
from langchain.vectorstores import Chroma
from langchain.embeddings import OpenAIEmbeddings

embeddings = OpenAIEmbeddings()
vectorstore = Chroma.from_documents(docs, embeddings)
llm = OpenAI()
chain = RetrievalQA.from_chain_type(llm, retriever=vectorstore.as_retriever())
# No built-in failover, cost tracking, or hallucination detection
```

```csharp
// Hazina - 4 lines, native C#
var ai = QuickSetup.SetupWithFailover(openAiKey, anthropicKey); // Auto-failover
var rag = new RAGEngine(ai, vectorStore);
await rag.IndexDocumentsAsync(docs);
var answer = await rag.QueryAsync("question"); // Cost tracked automatically
```

### vs Semantic Kernel

```csharp
// Semantic Kernel - requires plugins, manual setup
var kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion("gpt-4", apiKey)
    .Build();
// Failover? Add another plugin. Cost tracking? Write it yourself.
```

```csharp
// Hazina - batteries included
var ai = QuickSetup.SetupWithFailover(openAiKey, anthropicKey);
ai.EnableCostTracking(budgetLimit: 10.00m);
ai.EnableHealthMonitoring();
// Failover, cost tracking, health checks — all built-in
```

### vs Rolling Your Own

| Feature | DIY Effort | Hazina |
|---------|-----------|--------|
| Multi-provider abstraction | 2-4 weeks | ✅ Included |
| Circuit breaker + failover | 1-2 weeks | ✅ Included |
| Hallucination detection | 2-4 weeks | ✅ Included |
| Cost tracking + budgets | 1 week | ✅ Included |
| RAG with chunking | 2-3 weeks | ✅ Included |
| Agent workflows | 3-4 weeks | ✅ Included |
| Production monitoring | 1-2 weeks | ✅ Included |

**Total: 12-19 weeks of work → 0 with Hazina**

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Your Application                          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Hazina.AI.FluentAPI                          │
│  Hazina.AI() → .WithProvider() → .WithFaultDetection() → Ask()  │
└─────────────────────────────────────────────────────────────────┘
          │              │              │              │
          ▼              ▼              ▼              ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│   Providers  │ │    RAG       │ │   Agents     │ │  Neurochain  │
│  OpenAI      │ │  Indexing    │ │  Tools       │ │  Multi-layer │
│  Anthropic   │ │  Retrieval   │ │  Workflows   │ │  Reasoning   │
│  Local LLMs  │ │  Generation  │ │  Coordination│ │  Validation  │
└──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘
          │              │              │              │
          └──────────────┴──────────────┴──────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  Production Monitoring                           │
│         Metrics • Cost Tracking • Health Checks                  │
└─────────────────────────────────────────────────────────────────┘
```

## Core Capabilities

### Multi-Provider Orchestration
```csharp
var ai = QuickSetup.SetupWithFailover(openAiKey, anthropicKey);

// Automatic failover when primary fails
var response = await ai.GetResponse(messages); // Uses OpenAI, fails over to Claude

// Or select by strategy
ai.SetDefaultStrategy(SelectionStrategy.LeastCost);     // Cheapest provider
ai.SetDefaultStrategy(SelectionStrategy.FastestResponse); // Fastest provider
```

### Fault Detection & Hallucination Prevention
```csharp
var result = await Hazina.AI()
    .WithFaultDetection(minConfidence: 0.9)
    .Ask("What is the capital of France?")
    .ExecuteAsync();

// Automatically validates responses
// Detects hallucinations
// Retries with refined prompts if needed
```

### RAG (Retrieval-Augmented Generation)
```csharp
var rag = new RAGEngine(ai, vectorStore);

// Index documents with smart chunking
await rag.IndexDocumentsAsync(documents);

// Query with automatic context retrieval
var response = await rag.QueryAsync("Explain the authentication flow", new RAGQueryOptions
{
    TopK = 5,
    MinSimilarity = 0.7,
    RequireCitation = true
});
```

### Agentic Workflows
```csharp
var coordinator = new MultiAgentCoordinator();

coordinator.AddAgent(new Agent("researcher", researchPrompt, ai));
coordinator.AddAgent(new Agent("writer", writerPrompt, ai));
coordinator.AddAgent(new Agent("reviewer", reviewerPrompt, ai));

var result = await coordinator.ExecuteAsync("Write a blog post about AI",
    CoordinationStrategy.Sequential);
```

### Multi-Layer Reasoning (Neurochain)
```csharp
var neurochain = new NeuroChainOrchestrator();
neurochain.AddLayer(new FastReasoningLayer(ai));   // Quick analysis
neurochain.AddLayer(new DeepReasoningLayer(ai));   // Thorough analysis
neurochain.AddLayer(new VerificationLayer(ai));    // Cross-validation

var result = await neurochain.ReasonAsync("Complex question requiring high confidence");
// Returns 95-99% confidence through independent validation
```

## Documentation

📚 **[View Full Documentation](docs/apidoc/index.html)** — Complete API reference and guides (open locally after cloning)

**For private repositories:** Documentation is committed to the repository at `docs/apidoc/` - no external hosting required.

### Getting Started
- **[Solutions Guide](SOLUTIONS.md)** — Choose the right solution file for your work (QuickStart, Core, AI, Tools, Apps)
- [30-Minute RAG Tutorial](docs/quickstart.md) — Build production RAG in 30 minutes
- **[API Reference](docs/apidoc/api/index.html)** — Complete API documentation

### Feature Guides
- [Knowledge Storage & Search Model](docs/KNOWLEDGE_STORAGE.md) — Agent-first architecture, metadata-driven storage
- [RAG Guide](docs/RAG_GUIDE.md) — Document indexing, chunking, retrieval
- [Agents Guide](docs/AGENTS_GUIDE.md) — Tool calling, workflows, coordination
- [Neurochain Guide](docs/NEUROCHAIN_GUIDE.md) — Multi-layer reasoning
- [Code Intelligence Guide](docs/CODE_INTELLIGENCE_GUIDE.md) — Refactoring, analysis
- [Production Monitoring Guide](docs/PRODUCTION_MONITORING_GUIDE.md) — Metrics, health checks

### Setup & Configuration
- [Supabase Setup](docs/SUPABASE_SETUP.md) — Cloud database backend
- [GitHub Pages Setup](GITHUB_PAGES_SETUP.md) — Automatic documentation deployment

### For Contributors
- [Documentation Guidelines](DOCUMENTATION_GUIDELINES.md) — XML documentation standards
- **Generate Docs Locally**: Run `.\generate-docs.ps1 -Serve` to preview at http://localhost:8080

## Quick Start

```bash
# Clone repository
git clone https://github.com/hazina-ai/hazina.git
cd hazina

# Choose your solution file (see SOLUTIONS.md for guidance)
# New to Hazina? Start with QuickStart.sln
dotnet restore Hazina.QuickStart.sln
dotnet build Hazina.QuickStart.sln

# Working on a specific area?
# dotnet build Hazina.AI.sln       # AI features
# dotnet build Hazina.Core.sln     # Infrastructure
# dotnet build Hazina.Tools.sln    # Tools & services
# dotnet build Hazina.Apps.sln     # Applications

# Full build (all 62 projects)
# dotnet build Hazina.sln

# Run demos
dotnet run --project apps/Demos/Hazina.Demo.Supabase
```

## Definition of Done (DoD)

All contributions to Hazina must meet the complete Definition of Done before being merged.

**See:** `C:\scripts\_machine\DEFINITION_OF_DONE.md` for the comprehensive checklist (Brand2Boost/client-manager project context).

**Key Hazina-Specific DoD Requirements:**
1. ✅ Branch created from `develop` (or `main`)
2. ✅ Code implemented with tests (≥80% coverage for new code)
3. ✅ Example code updated to reflect new features
4. ✅ PR created, reviewed, and merged
5. ✅ NuGet package versioned (if public release)
6. ✅ Breaking changes documented in MIGRATION_GUIDE.md
7. ✅ Client-manager compatibility verified (if applicable)
8. ✅ Documentation updated (README, API docs, guides)

**Key Principle:** A contribution is NOT done until it's merged, deployed (if applicable), and documented.

---

## Documentation

Hazina provides comprehensive documentation to help you get started and master the framework.

### Local Documentation (Private Repositories)

Documentation is auto-generated and committed to the repository at **`docs/apidoc/`**:

- Open `docs/apidoc/index.html` in your browser for the full documentation
- **Getting Started** - Quickstart guides and tutorials
- **API Reference** - Complete API documentation at `docs/apidoc/api/index.html`
- **Architecture** - Design decisions and patterns
- **Guides** - RAG, agents, context engineering, and more

**Benefits for private repos:** No external hosting needed - documentation is version-controlled alongside code.

### Regenerating Documentation Locally

```bash
# Generate API documentation from source code
.\generate-docs.ps1

# Generate and preview in browser (http://localhost:8080)
.\generate-docs.ps1 -Serve

# Clean previous build and regenerate
.\generate-docs.ps1 -Clean
```

Documentation is automatically regenerated by GitHub Actions on every push to develop/main.

### Documentation Standards

All public APIs must include XML documentation comments. See [DOCUMENTATION_GUIDELINES.md](DOCUMENTATION_GUIDELINES.md) for details.

**Before creating a PR:**
- ✅ All public APIs have XML documentation
- ✅ Complex features have usage examples
- ✅ Documentation builds without errors: `.\generate-docs.ps1`
- ✅ No CS1591 warnings (missing XML comments)

---

## Contributing

We welcome contributions! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

**Before contributing:**
- Read the Definition of Done (see above)
- Ensure all DoD criteria can be met
- Create feature branches from `develop` (or `main`)
- Follow semantic versioning for breaking changes

## License

MIT License - see [LICENSE](LICENSE) for details.

---

**Built for .NET developers who ship production AI.**
