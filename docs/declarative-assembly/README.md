# Hazina Declarative RAG App Building System

**Status:** Planning
**Version:** 1.0
**Created:** 2026-01-13
**Author:** Claude Code Planning Agent

---

## Executive Summary

This document outlines the architecture and implementation plan for a **Declarative RAG Application Building System** that leverages Hazina's existing infrastructure to enable:

1. **One-Click Project Creation** - Visual Studio template for new Hazina RAG applications
2. **Declarative Assembly** - YAML/JSON specifications that define complete AI applications
3. **AI-Powered Code Generation** - Claude Code as the active build agent
4. **Modular Component System** - Swappable providers, pipelines, and modules

**Key Insight:** Hazina already provides the complete runtime layer. What's needed is the **representation layer** (specs, registry) and **generation layer** (scaffolder, templates).

---

## Why This Matters

### Current State (Without Declarative Assembly)

```
Developer → Read docs → Choose providers → Write DI config → Wire controllers → Test → Debug → Deploy
           └── 2-4 hours of boilerplate for every new project
```

### Future State (With Declarative Assembly)

```
Developer → Write YAML spec → Run scaffold → Build → Deploy
           └── 15 minutes from idea to running application
```

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                     Visual Studio Template                           │
│                "File → New → Hazina Modular RAG App"                 │
└─────────────────────────────────────────────────────────────────────┘
                                  │
                                  │ Uses
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      Scaffold Generator                              │
│           Consumes spec → Generates project structure                │
└─────────────────────────────────────────────────────────────────────┘
                                  │
                                  │ Reads
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Assembly Specification                            │
│            YAML/JSON declaring components + config                   │
└─────────────────────────────────────────────────────────────────────┘
                                  │
                                  │ References
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     Component Registry                               │
│    Machine-readable definitions of all Hazina components             │
└─────────────────────────────────────────────────────────────────────┘
                                  │
                                  │ Describes
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Hazina Runtime Layer                              │
│   ILLMClient │ IProviderOrchestrator │ IDocumentStore │ RAGEngine   │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Implementation Components

### 1. Component Registry

**Purpose:** Machine-readable catalog of all Hazina components that can be assembled into applications.

**Location:** `src/Core/AI/Hazina.AI.Assembly/Registry/`

**Key Files:**
- Component catalog (JSON/YAML)
- Schema definitions
- Runtime resolution

**Documentation:** [01-COMPONENT_REGISTRY.md](./01-COMPONENT_REGISTRY.md)

---

### 2. Assembly Specification

**Purpose:** Declarative format for defining complete AI applications without writing code.

**Location:** `src/Core/AI/Hazina.AI.Assembly/Specification/`

**Format:** YAML with JSON Schema validation

**Documentation:** [02-ASSEMBLY_SPECIFICATION.md](./02-ASSEMBLY_SPECIFICATION.md)

---

### 3. Scaffold Generator

**Purpose:** Consumes assembly specifications and generates complete, runnable projects.

**Location:** `src/Core/AI/Hazina.AI.Assembly/Generator/`

**Outputs:**
- Complete project structure
- DI configuration
- Controllers
- appsettings.json
- Docker configuration

**Documentation:** [03-SCAFFOLD_GENERATOR.md](./03-SCAFFOLD_GENERATOR.md)

---

### 4. Visual Studio Template

**Purpose:** "File → New Project" experience for creating Hazina applications.

**Location:** `templates/project-templates/Hazina.RAG.Template/`

**Deliverables:**
- VSIX package
- dotnet new template
- Visual Studio wizard

**Documentation:** [04-VS_TEMPLATE.md](./04-VS_TEMPLATE.md)

---

### 5. AI Build Orchestrator

**Purpose:** Claude Code instructions for AI-driven application assembly.

**Location:** Control plane integration (C:\scripts)

**Capabilities:**
- Parse specifications
- Select components
- Apply templates
- Validate builds
- Auto-fix errors

**Documentation:** [05-AI_BUILD_ORCHESTRATOR.md](./05-AI_BUILD_ORCHESTRATOR.md)

---

## Relationship to Existing Hazina Features

### What Already Exists (Leverage)

| Feature | Interface | Use Case |
|---------|-----------|----------|
| LLM Providers | ILLMClient, IProviderOrchestrator | Multi-provider failover |
| RAG Pipeline | IRetrievalPipeline, IRetriever, IReranker | Document retrieval |
| Document Storage | IDocumentStore, IEmbeddingStore | Content persistence |
| Graph RAG | IGraphStore, IEntityExtractor | Knowledge graphs |
| Context Engineering | IContextEngine, IContextRetriever | Multi-source context |
| Memory | IMemoryModule, ISemanticMemoryStore | Agent memory |
| Agents | AgentFactory, WorkflowEngine | Agentic workflows |
| Observability | Metrics, Logging, Tracing | Production monitoring |

### What We're Adding (Build)

| Feature | Purpose | Location |
|---------|---------|----------|
| Component Registry | Catalog all components | `Hazina.AI.Assembly` |
| Assembly Spec | Declarative app definitions | `Hazina.AI.Assembly` |
| Scaffold Generator | Project generation | `Hazina.AI.Assembly` |
| VS Template | One-click creation | `templates/` |
| AI Orchestrator | Claude Code integration | Control plane |

---

## Quick Start Example

### Step 1: Write Assembly Specification

```yaml
# my-rag-app.assembly.yaml
metadata:
  name: my-document-assistant
  version: 1.0.0
  description: RAG assistant for company documents

providers:
  llm:
    primary:
      type: embedding.openai
      model: gpt-4o
    fallback:
      type: embedding.anthropic
      model: claude-3-5-sonnet

  embedding:
    type: embedding.openai
    model: text-embedding-3-large
    dimensions: 1536

  storage:
    documents:
      type: storage.local
      path: ./documents
    vectors:
      type: vector.supabase
      connection: ${SUPABASE_URL}

pipelines:
  ingestion:
    - chunker:
        type: chunk.semantic
        size: 512
        overlap: 50
    - embedder:
        provider: embedding
    - indexer:
        store: vectors

  retrieval:
    - retriever:
        type: retrieval.vector
        topK: 20
        minSimilarity: 0.7
    - reranker:
        type: rerank.llm
        topN: 5

modules:
  - type: module.rag-query
    endpoint: /api/query
  - type: module.document-ingest
    endpoint: /api/ingest
  - type: module.health
    endpoint: /health

output:
  type: webapi
  framework: aspnet
  features:
    - swagger
    - cors
    - auth.jwt
```

### Step 2: Generate Project

```bash
# Using CLI
hazina assemble my-rag-app.assembly.yaml --output ./MyRagApp

# Using dotnet template
dotnet new hazina-rag --name MyRagApp --spec my-rag-app.assembly.yaml
```

### Step 3: Build and Run

```bash
cd MyRagApp
dotnet restore
dotnet run
```

---

## Implementation Roadmap

### Phase 1: Foundation (Weeks 1-4)
- Component Registry design and implementation
- Assembly Specification schema
- Basic scaffold generator
- **Milestone:** Generate working project from spec

### Phase 2: Completeness (Weeks 5-8)
- Full provider coverage
- Pipeline composition
- Module library
- **Milestone:** All Hazina features accessible via spec

### Phase 3: Developer Experience (Weeks 9-12)
- Visual Studio template
- CLI tooling
- Validation and error messages
- **Milestone:** One-click project creation

### Phase 4: AI Integration (Weeks 13-16)
- Claude Code orchestrator
- Auto-fix capabilities
- Spec generation from natural language
- **Milestone:** AI-driven app building

---

## Success Criteria

### Phase 1
- [ ] Component registry covers 80% of Hazina features
- [ ] Assembly spec can express common RAG patterns
- [ ] Generated projects compile and run

### Phase 2
- [ ] 100% interface coverage in registry
- [ ] Complex multi-provider apps supported
- [ ] Pipeline composition working

### Phase 3
- [ ] VS template published to marketplace
- [ ] dotnet new template available
- [ ] < 5 minutes from spec to running app

### Phase 4
- [ ] Claude Code can generate specs from descriptions
- [ ] Auto-fix handles 80% of common errors
- [ ] Documentation generated automatically

---

## Document Index

1. [Component Registry](./01-COMPONENT_REGISTRY.md) - Catalog design
2. [Assembly Specification](./02-ASSEMBLY_SPECIFICATION.md) - YAML format
3. [Scaffold Generator](./03-SCAFFOLD_GENERATOR.md) - Code generation
4. [Visual Studio Template](./04-VS_TEMPLATE.md) - Project templates
5. [AI Build Orchestrator](./05-AI_BUILD_ORCHESTRATOR.md) - Claude integration

---

## Next Steps

1. **Review this document** - Get stakeholder alignment
2. **Start Phase 1** - Begin Component Registry implementation
3. **Create project structure** - `Hazina.AI.Assembly` project
4. **Write first component definitions** - Start with LLM providers

---

**Last Updated:** 2026-01-13
**Status:** Ready for Review
