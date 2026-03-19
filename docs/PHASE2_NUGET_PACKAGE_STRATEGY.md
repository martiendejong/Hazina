# Hazina NuGet Package Strategy - Phase 2

**Date:** 2026-03-19
**Task:** 869cfzy8b - Hazina Modular Refactoring Phase 2: NuGet Package Strategy
**Author:** Hazina Team
**Status:** Complete Strategy Document

---

## Executive Summary

This document defines the comprehensive NuGet packaging strategy for Hazina framework. The strategy maintains the monorepo structure while enabling modular consumption through well-defined NuGet packages organized into 5 categories: **Core**, **AI**, **LLM Providers**, **Tools**, and **Infrastructure**.

### Key Strategy Points

1. **Monorepo Architecture** - Keep all code in single repository for unified development
2. **Modular Packages** - Publish 108+ independent NuGet packages for flexible consumption
3. **Clear Layering** - 5 package categories aligned with architectural layers (Phase 1)
4. **Semantic Versioning** - Independent versioning per package with GitVersion automation
5. **Multi-Targeting** - Support .NET 8.0, 9.0, and 10.0 (completed in Phase 4)
6. **Meta-Packages** - Convenience packages bundling common dependencies
7. **Local Development** - Local NuGet feed for testing before public release

---

## Package Categories & Taxonomy

### Category 1: Core Foundation (12 packages)

**Namespace:** `Hazina.LLMs.*`, `Hazina.Store.*`, `Hazina.AI.Providers`

These are the most stable packages with the highest dependency counts. Zero Hazina dependencies (except internal).

| Package | Dependents | Description | Target Frameworks |
|---------|-----------|-------------|-------------------|
| **Hazina.LLMs.Client** | 49 | Core `ILLMClient` interface - provider-agnostic LLM abstraction | net8.0;net9.0;net10.0 |
| **Hazina.LLMs.Classes** | 36 | Shared DTOs (ChatMessage, ChatResponse, CompletionRequest) | net8.0;net9.0;net10.0 |
| **Hazina.LLMs.Helpers** | 20 | Token counting, text formatting, chunking utilities | net8.0;net9.0;net10.0 |
| **Hazina.LLMs.Tools** | - | Tool calling abstractions and utilities | net8.0;net9.0;net10.0 |
| **Hazina.LLMClientTools** | 8 | Helper tools for LLM client implementations | net8.0;net9.0;net10.0 |
| **Hazina.Store.EmbeddingStore** | 19 | Vector storage abstraction (embeddings) | net8.0;net9.0;net10.0 |
| **Hazina.Store.DocumentStore** | 16 | Document storage abstraction | net8.0;net9.0;net10.0 |
| **Hazina.Store.FactsStore** | - | Knowledge/facts storage | net8.0;net9.0;net10.0 |
| **Hazina.Store.Sqlite** | - | SQLite implementation of stores | net8.0;net9.0;net10.0 |
| **Hazina.Tools.Core** | 19 | Base tool infrastructure | net8.0;net9.0;net10.0 |
| **Hazina.Tools.Data** | 26 | Data access patterns | net8.0;net9.0;net10.0 |
| **Hazina.Tools.Models** | 25 | Domain models | net8.0;net9.0;net10.0 |

**Version Strategy:** Major version changes only (1.x → 2.x for breaking changes). High stability guarantee.

**Dependencies:** Only external packages (System.*, Microsoft.Extensions.*, etc.). Zero inter-Hazina dependencies.

---

### Category 2: LLM Providers (8 packages)

**Namespace:** `Hazina.LLMs.*`

Perfect modularity example - zero cross-dependencies. All implement `ILLMClient` from `Hazina.LLMs.Client`.

| Package | Models | Dependencies | Version |
|---------|--------|--------------|---------|
| **Hazina.LLMs.OpenAI** | GPT-4, GPT-3.5, DALL-E | Client, Classes, Helpers | 2.0.0 |
| **Hazina.LLMs.Anthropic** | Claude 3 Opus/Sonnet/Haiku | Client, Classes | 1.0.0 |
| **Hazina.LLMs.Gemini** | Gemini Pro/Ultra/Flash | Client, Classes | 1.0.0 |
| **Hazina.LLMs.GoogleADK** | Google AI SDK wrapper | Client, Classes | 1.0.0 |
| **Hazina.LLMs.Mistral** | Mistral Large/Medium/Small | Client, Classes | 1.0.0 |
| **Hazina.LLMs.HuggingFace** | HuggingFace Inference API | Client, Classes | 1.0.0 |
| **Hazina.LLMs.Ollama** | Local Llama/Mistral/Phi | Client, Classes | 1.0.0 |
| **Hazina.LLMs.SemanticKernel** | Microsoft Semantic Kernel integration | Client, Classes, SK packages | 1.0.0 |

**Version Strategy:** Independent versioning per provider. Breaking changes in provider APIs only affect that package.

**Usage Pattern:**
```csharp
// Users only install providers they need
dotnet add package Hazina.LLMs.OpenAI
dotnet add package Hazina.LLMs.Anthropic
```

---

### Category 3: AI Core (28 packages)

**Namespace:** `Hazina.AI.*`, `Hazina.Neurochain.*`, `Hazina.*`

Core AI capabilities built on foundation layer.

#### AI Infrastructure (5 packages)
| Package | Description | Key Dependencies |
|---------|-------------|------------------|
| **Hazina.AI.Core** | Base AI abstractions | LLMs.Client |
| **Hazina.AI.Orchestration** | Request routing, failover | AI.Providers |
| **Hazina.AI.FluentAPI** | Fluent configuration API | AI.Orchestration |
| **Hazina.AI.Providers** | Multi-provider coordination | LLMs.Client, LLMs.Classes |
| **Hazina.LLMs.Registry** | Dynamic provider registration | AI.Providers |

#### RAG & Context (4 packages)
| Package | Description | Key Dependencies |
|---------|-------------|------------------|
| **Hazina.AI.RAG** | Retrieval-Augmented Generation | Store.EmbeddingStore, Neurochain |
| **Hazina.AI.ContextEngineering** | Context window optimization | LLMs.Helpers |
| **Hazina.AI.Compression** | Token compression | LLMs.Helpers |
| **Hazina.LongContext** | Long context handling (100K+ tokens) | AI.Compression |

#### Reasoning & Validation (3 packages)
| Package | Description | Key Dependencies |
|---------|-------------|------------------|
| **Hazina.Neurochain.Core** | Multi-layer validation pipeline | AI.Providers |
| **Hazina.AI.FaultDetection** | Hallucination detection | Neurochain.Core |
| **Hazina.CodeIntelligence** | Code analysis and generation | AI.Providers |

#### Agents (6 packages)
| Package | Description | Key Dependencies |
|---------|-------------|------------------|
| **Hazina.AI.Agents** | Multi-agent coordination | AI.Orchestration |
| **Hazina.AI.Workflows** | Workflow orchestration | AI.Agents |
| **Hazina.AI.Guardrails** | Safety constraints & limits | AI.Agents |
| **Hazina.AgentFactory** | Agent creation & management | AI.Agents, Generator, Store |
| **Hazina.Generator** | Code/content generation | LLMs.OpenAI |
| **Hazina.DynamicAPI** | Dynamic API generation | Generator |

#### Specialized AI (5 packages)
| Package | Description | Key Dependencies |
|---------|-------------|------------------|
| **Hazina.AI.Vision** | Computer vision (ImageSharp, FFMpeg) | LLMs.OpenAI |
| **Hazina.AI.Training** | Fine-tuning with TorchSharp | LLMs.Client |
| **Hazina.AI.Inference** | ONNX Runtime inference | Microsoft.ML.OnnxRuntime |
| **Hazina.AI.LocalLLM** | LLamaSharp integration | LLamaSharp |
| **Hazina.Brain** | Integrated AI system | Multiple AI packages |

#### Quality & Management (5 packages)
| Package | Description | Key Dependencies |
|---------|-------------|------------------|
| **Hazina.AI.Learning** | Feedback loops & reinforcement | AI.Agents |
| **Hazina.Quality** | Quality assurance | Evals |
| **Hazina.Evals** | Evaluation frameworks | AI.Providers |
| **Hazina.AI.Routing** | Intelligent request routing | AI.Providers |
| **Hazina.AI.TaskPrediction** | Task prediction & scheduling | AI.Learning |

**Version Strategy:** Follow semantic versioning. Breaking changes in one AI package may require coordinated releases.

---

### Category 4: Tools & Services (45+ packages)

**Namespace:** `Hazina.Tools.*`

#### Tools Foundation (8 packages)
| Package | Description |
|---------|-------------|
| **Hazina.Tools.Common.Models** | Common models |
| **Hazina.Tools.Common.Infrastructure.AspNetCore** | ASP.NET Core tooling |
| **Hazina.Tools.Models** | Domain models |
| **Hazina.Tools.Extensions** | Extension methods |
| **Hazina.Tools.Core** | Core tool abstractions |
| **Hazina.Tools.TextExtraction** | Text extraction utilities |
| **Hazina.Tools.Data** | Data access patterns |
| **Hazina.Tools.AI.Agents** | AI agent tooling |

#### Tools Services (17+ packages)
| Package | Description |
|---------|-------------|
| **Hazina.Tools.Services** | Base services infrastructure |
| **Hazina.Tools.Services.Embeddings** | Embedding generation |
| **Hazina.Tools.Services.Store** | Storage services |
| **Hazina.Tools.Services.FileOps** | File operations |
| **Hazina.Tools.Services.Web** | Web scraping/crawling |
| **Hazina.Tools.Services.WebSearch** | Multi-engine search (Google, Bing, DDG) |
| **Hazina.Tools.Services.Chat** | Chat integrations |
| **Hazina.Tools.Services.Images** | Image processing |
| **Hazina.Tools.Services.Database** | Database operations |
| **Hazina.Tools.Services.Social** | Social media APIs |
| **Hazina.Tools.Services.ContentRetrieval** | Content fetching |
| **Hazina.Tools.Services.DataGathering** | Data collection |
| **Hazina.Tools.Services.Intake** | Data ingestion |
| **Hazina.Tools.Services.Prompts** | Prompt management |
| **Hazina.Tools.Services.BigQuery** | Google BigQuery integration |
| **Hazina.Tools.Services.WordPress** | WordPress API |
| **Hazina.Tools.Services.GoogleDrive** | Google Drive integration |

#### WebSearch Module (4 packages) - Best Practice Example
```
Hazina.Tools.Services.WebSearch (umbrella package)
├── WebSearch.Core (interfaces)
├── WebSearch.Infrastructure (base implementation)
└── WebSearch.Providers (Google, Bing, DuckDuckGo providers)
```

#### Development Tools (3 packages)
| Package | Description |
|---------|-------------|
| **Hazina.Tools.CsAutofix** | C# code auto-fixing |
| **Hazina.Tools.UIAutomationBridge** | UI automation bridge |
| **Hazina.Tools.WorkTray** | Task tray utilities |

#### Production (1 package)
| Package | Description |
|---------|-------------|
| **Hazina.Production.Monitoring** | Production monitoring & telemetry |

**Version Strategy:** Independent versioning. Service packages can evolve independently.

---

### Category 5: Infrastructure (23 packages)

**Namespace:** `Hazina.Auth.*`, `Hazina.Security.*`, `Hazina.Observability.*`, etc.

#### Auth (2 packages)
| Package | Description |
|---------|-------------|
| **Hazina.Auth.Core** | Core authentication abstractions |
| **Hazina.Auth.Identity** | ASP.NET Identity integration |

#### Security (2 packages)
| Package | Description |
|---------|-------------|
| **Hazina.Security.Core** | Security abstractions |
| **Hazina.Security.AspNetCore** | ASP.NET Core security middleware |

#### Observability (3 packages)
| Package | Description |
|---------|-------------|
| **Hazina.Observability.Core** | Core observability abstractions |
| **Hazina.Observability.AspNetCore** | ASP.NET Core telemetry |
| **Hazina.Observability.LLMLogs** | LLM request/response logging |

#### API (3 packages)
| Package | Description |
|---------|-------------|
| **Hazina.API.Generic** | Generic API utilities |
| **Hazina.Terminal.API** | Terminal API services |
| **Hazina.Terminal.ChatAgent** | Terminal chat agent |

#### Other Infrastructure (13 packages)
| Package | Description |
|---------|-------------|
| **Hazina.EventSourcing** | Event sourcing patterns |
| **Hazina.CodeGeneration.Core** | Code generation utilities |
| **Hazina.Enterprise.Core** | Enterprise features |
| **Hazina.Indexing** | Indexing services |
| **Hazina.Core.Plugins** | Plugin system |
| **Hazina.ChatShared** | Shared chat UI components |
| **Hazina.UI.SchemaComponents** | Schema-based UI components |
| **Hazina.AgenticOrchestration** | Agentic orchestration |
| **Hazina.TaskRunner** | Task runner infrastructure |
| **Hazina.TaskRunner.UI** | Task runner UI |
| **Hazina.Agent.API** | Agent API services |
| **Hazina.Agents.Tools** | Agent tooling |
| **Hazina.Agents.Coding** | Coding agent specializations |

**Version Strategy:** Infrastructure packages follow semantic versioning with coordinated major version bumps.

---

## Meta-Packages (Convenience Bundles)

Meta-packages provide convenient installation of related packages for common use cases.

### 1. **Hazina** (Main Meta-Package)

**Description:** Complete Hazina framework - everything you need to get started

**Includes:**
- Core Foundation (12 packages)
- AI Core Infrastructure (Orchestration, FluentAPI, Providers)
- RAG & Context (AI.RAG, ContextEngineering, Compression)
- OpenAI Provider (default)
- Essential Tools (FileOps, Web, Database)
- Security & Observability

**Target Audience:** New users, rapid prototyping, full-featured applications

**Installation:**
```bash
dotnet add package Hazina
```

**Version:** Tracks the latest stable release across all components

---

### 2. **Hazina.Core** (Minimal Foundation)

**Description:** Minimal foundation for building custom LLM applications

**Includes:**
- Hazina.LLMs.Client
- Hazina.LLMs.Classes
- Hazina.LLMs.Helpers
- Hazina.AI.Providers
- Hazina.Store.EmbeddingStore
- Hazina.Store.DocumentStore

**Target Audience:** Advanced users building custom solutions, minimal dependencies

**Installation:**
```bash
dotnet add package Hazina.Core
```

---

### 3. **Hazina.AI.Complete** (All AI Features)

**Description:** Complete AI capabilities (RAG, Agents, Reasoning, Vision, Training)

**Includes:**
- All AI Core packages (28 packages)
- AgentFactory, Generator, DynamicAPI
- Neurochain, FaultDetection, CodeIntelligence

**Target Audience:** AI-heavy applications, research projects

**Installation:**
```bash
dotnet add package Hazina.AI.Complete
```

---

### 4. **Hazina.Providers.All** (All LLM Providers)

**Description:** All LLM provider implementations

**Includes:**
- All 8 LLM provider packages (OpenAI, Anthropic, Gemini, etc.)

**Target Audience:** Multi-provider applications, failover scenarios

**Installation:**
```bash
dotnet add package Hazina.Providers.All
```

---

### 5. **Hazina.Tools.Complete** (All Tools & Services)

**Description:** Complete tooling suite (45+ packages)

**Includes:**
- All Tools Foundation (8 packages)
- All Tools Services (17+ packages)
- Development Tools (3 packages)
- Production Monitoring

**Target Audience:** Tool-heavy applications, web scraping, content processing

**Installation:**
```bash
dotnet add package Hazina.Tools.Complete
```

---

### 6. **Hazina.Web** (Web Application Bundle)

**Description:** Everything needed for web applications

**Includes:**
- Hazina.Core
- Hazina.Security.AspNetCore
- Hazina.Observability.AspNetCore
- Hazina.Tools.Services.Web
- Hazina.Tools.Services.Chat
- Hazina.API.Generic

**Target Audience:** ASP.NET Core web applications

**Installation:**
```bash
dotnet add package Hazina.Web
```

---

## Versioning Strategy

### Semantic Versioning Rules

All Hazina packages follow [Semantic Versioning 2.0.0](https://semver.org/):

```
MAJOR.MINOR.PATCH
```

- **MAJOR** - Breaking changes (API changes, removed features)
- **MINOR** - New features (backward compatible)
- **PATCH** - Bug fixes (backward compatible)

### Version Synchronization

**Independent Versioning:**
- Core Foundation packages (1.x.x) - Highest stability
- LLM Providers (independent) - Version per provider
- Tools & Services (independent) - Version per service
- Infrastructure (synchronized) - Major versions coordinated

**Example:**
```
Hazina.LLMs.Client: 2.0.0 (stable)
Hazina.LLMs.OpenAI: 2.1.3 (frequent updates)
Hazina.LLMs.Anthropic: 1.0.5 (less frequent)
Hazina.AI.RAG: 1.2.1 (moderate updates)
Hazina.Tools.Services.WebSearch: 1.5.0 (independent)
```

### GitVersion Automation

**Configuration:** `GitVersion.yml` in repository root

```yaml
mode: ContinuousDeployment
tag-prefix: '[vV]'
continuous-delivery-fallback-tag: ci
branches:
  main:
    regex: ^main$
    mode: ContinuousDelivery
    tag: ''
    increment: Patch
    is-mainline: true
  develop:
    regex: ^develop$
    mode: ContinuousDeployment
    tag: alpha
    increment: Minor
  feature:
    regex: ^features?[/-]
    mode: ContinuousDeployment
    tag: beta
    increment: Minor
  release:
    regex: ^releases?[/-]
    mode: ContinuousDeployment
    tag: rc
    increment: Patch
```

**Usage:**
```bash
# Automatic version calculation
dotnet tool install --global GitVersion.Tool
dotnet-gitversion /showvariable SemVer
```

### Per-Package Versioning

**Option 1: Manual (Current)**
- Each `.csproj` has explicit `<Version>` tag
- Update manually or via script before release
- Simple, explicit, full control

**Option 2: GitVersion (Recommended for CI/CD)**
- Calculate version from git history
- Tag releases: `v1.2.3`, `v1.2.3-provider-openai`
- Automatic in CI/CD pipeline

**Option 3: Hybrid (Best for Hazina)**
- Core Foundation: Manual (rare changes)
- Providers & Services: GitVersion (frequent updates)
- Infrastructure: Manual (coordinated releases)

---

## Dependency Management

### Dependency Hierarchy

```
Layer 6: Applications (no packages)
         ↓
Layer 5: Infrastructure (23 packages)
         ↓
Layer 4: Tools & Services (45+ packages)
         ↓
Layer 3: LLM Providers (8 packages)
         ↓
Layer 2: AI Core (28 packages)
         ↓
Layer 1: Core Foundation (12 packages)
         ↓
External Dependencies (Microsoft.*, System.*)
```

**Rule:** Lower layers NEVER depend on upper layers.

### External Dependencies

**Shared Dependencies** (via Directory.Build.props):
```xml
<PackageReference Include="Microsoft.Extensions.Configuration" Version="10.0.3" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.3" />
```

**Version Centralization:**
- All `Microsoft.Extensions.*` versions synchronized
- Defined once in `Directory.Build.props`
- Inherited by all projects

**Heavy Dependencies** (isolated to specific packages):
```
TorchSharp → Hazina.AI.Training only
LLamaSharp → Hazina.AI.LocalLLM only
Microsoft.ML.OnnxRuntime → Hazina.AI.Inference only
ImageSharp + FFMpeg → Hazina.AI.Vision only
```

### Avoiding Diamond Dependencies

**Problem:** Package A and B both depend on Package C (different versions)

**Solutions:**
1. **Version Ranges** - Allow compatible version ranges
   ```xml
   <PackageReference Include="Hazina.LLMs.Client" Version="[2.0.0, 3.0.0)" />
   ```

2. **Central Package Management** - Use `Directory.Packages.props`
   ```xml
   <PackageVersion Include="Hazina.LLMs.Client" Version="2.0.0" />
   ```

3. **Frequent Updates** - Keep dependencies up-to-date

---

## Local Development Workflow

### Local NuGet Feed

**Purpose:** Test packages before public release, internal development

**Location:** `C:\nuget-local\` (Windows) or `~/nuget-local/` (Linux/Mac)

**Setup:**
```bash
# Create local feed directory
mkdir C:\nuget-local

# Add to NuGet sources
dotnet nuget add source C:\nuget-local --name Local

# List sources to verify
dotnet nuget list source
```

### Pack & Test Locally

**Script:** `scripts/pack-local.ps1`

```powershell
# Pack all packages to local feed
.\scripts\pack-local.ps1

# Test in consumer project
cd C:\MyApp
dotnet add package Hazina.AI.RAG --source Local
dotnet restore
```

### Local Development Loop

1. **Make Changes** - Edit code in Hazina repo
2. **Pack Locally** - Run `pack-local.ps1` (increments version with `-local` suffix)
3. **Test** - Reference from consumer project with `--source Local`
4. **Iterate** - Repeat until satisfied
5. **Commit** - Commit changes to git
6. **CI/CD** - Automated tests, pack, publish to NuGet.org

---

## CI/CD Integration

### GitHub Actions Workflow

**File:** `.github/workflows/nuget-publish.yml`

```yaml
name: Publish NuGet Packages

on:
  push:
    tags:
      - 'v*.*.*'
  workflow_dispatch:
    inputs:
      version:
        description: 'Version to publish (e.g., 1.2.3)'
        required: true

jobs:
  publish:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v3
      with:
        fetch-depth: 0  # Full history for GitVersion

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: |
          8.0.x
          9.0.x
          10.0.x

    - name: Install GitVersion
      run: dotnet tool install --global GitVersion.Tool

    - name: Calculate Version
      id: gitversion
      run: |
        $version = dotnet-gitversion /showvariable SemVer
        echo "version=$version" >> $env:GITHUB_OUTPUT

    - name: Restore dependencies
      run: dotnet restore Hazina.sln

    - name: Build
      run: dotnet build Hazina.sln --configuration Release --no-restore

    - name: Pack NuGet packages
      run: .\scripts\publish-nuget.ps1 -Version ${{ steps.gitversion.outputs.version }} -DryRun

    - name: Publish to NuGet.org
      run: .\scripts\publish-nuget.ps1 -ApiKey ${{ secrets.NUGET_API_KEY }} -Version ${{ steps.gitversion.outputs.version }}
      env:
        NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
```

### Release Process

1. **Create Release Branch** - `git checkout -b release/1.2.0`
2. **Update Versions** - Update `.csproj` versions or rely on GitVersion
3. **Update CHANGELOG** - Document changes
4. **Merge to Main** - Pull request + review
5. **Tag Release** - `git tag v1.2.0 && git push --tags`
6. **CI/CD Triggers** - GitHub Actions builds, packs, publishes
7. **Verify** - Check NuGet.org for published packages

---

## Package Metadata Standards

### Required Metadata (in .csproj)

```xml
<PropertyGroup>
  <!-- Identity -->
  <PackageId>Hazina.AI.RAG</PackageId>
  <Version>1.2.0</Version>

  <!-- Description -->
  <Title>Hazina RAG - Retrieval-Augmented Generation</Title>
  <Description>Production-ready RAG implementation with multi-layer validation, semantic search, and context optimization</Description>
  <PackageTags>ai;llm;rag;retrieval;augmented;generation;semantic-search;embeddings</PackageTags>

  <!-- Legal -->
  <Authors>Hazina Team</Authors>
  <Company>Hazina</Company>
  <Copyright>Copyright © Hazina 2024-2026</Copyright>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>

  <!-- Links -->
  <PackageProjectUrl>https://github.com/martiendejong/Hazina</PackageProjectUrl>
  <RepositoryUrl>https://github.com/martiendejong/Hazina.git</RepositoryUrl>
  <RepositoryType>git</RepositoryType>

  <!-- Assets -->
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageIcon>icon.png</PackageIcon>

  <!-- Build -->
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
</PropertyGroup>
```

### Package README

**File:** `README.md` in each project directory

**Template:**
```markdown
# Hazina.[PackageName]

[One-line description]

## Installation

```bash
dotnet add package Hazina.[PackageName]
```

## Quick Start

[Minimal code example]

## Features

- Feature 1
- Feature 2
- Feature 3

## Documentation

Full documentation: https://docs.hazina.dev/[package-name]

## License

MIT - see LICENSE file
```

---

## Migration Path (From Current State)

### Current State (as of 2026-03-19)

- ✅ 108 projects with `.csproj` files
- ✅ 99 packages published to local feed (`local_packages/`)
- ✅ `Directory.Build.props` with shared metadata
- ✅ Multi-targeting (net8.0;net9.0;net10.0) completed
- ✅ `scripts/publish-nuget.ps1` exists (118 packages listed)
- ✅ Package metadata in most `.csproj` files
- ⚠️ Version numbers inconsistent (1.0.0, 1.0.1, 2.0.0)
- ⚠️ Some packages missing descriptions/tags
- ❌ No GitVersion configuration
- ❌ No CI/CD automation
- ❌ No meta-packages defined

### Phase 2 Implementation Steps

#### Step 1: Audit & Standardize Metadata ✅ (This Document)

**Deliverables:**
- ✅ This strategy document
- ✅ Package taxonomy (5 categories)
- ✅ Meta-package definitions (6 bundles)
- ✅ Versioning strategy

**Duration:** Completed

---

#### Step 2: Update Project Files (Next)

**Tasks:**
- [ ] Audit all 108 `.csproj` files
- [ ] Ensure all have complete metadata (see template above)
- [ ] Standardize versions (decide: 1.0.0 reset or keep current)
- [ ] Add missing `README.md` files (one per package)
- [ ] Add package icon (`icon.png` in repo root)

**Script:** `scripts/audit-package-metadata.ps1`

**Duration:** 2-3 hours

---

#### Step 3: Create Meta-Packages

**Tasks:**
- [ ] Create 6 meta-package `.csproj` files:
  - `src/Meta/Hazina/Hazina.csproj` (main bundle)
  - `src/Meta/Hazina.Core/Hazina.Core.csproj` (minimal)
  - `src/Meta/Hazina.AI.Complete/Hazina.AI.Complete.csproj`
  - `src/Meta/Hazina.Providers.All/Hazina.Providers.All.csproj`
  - `src/Meta/Hazina.Tools.Complete/Hazina.Tools.Complete.csproj`
  - `src/Meta/Hazina.Web/Hazina.Web.csproj`
- [ ] Define dependencies (all `<PackageReference Include="Hazina.*" />`)
- [ ] Test locally

**Duration:** 1 hour

---

#### Step 4: Local Feed Automation

**Tasks:**
- [ ] Create `scripts/pack-local.ps1`
- [ ] Configure local NuGet source (`C:\nuget-local\`)
- [ ] Pack all 108+ packages to local feed
- [ ] Test meta-packages in sample project

**Script:** `scripts/pack-local.ps1`

**Duration:** 1 hour

---

#### Step 5: GitVersion Configuration

**Tasks:**
- [ ] Create `GitVersion.yml` in repo root
- [ ] Configure branching strategy (main, develop, feature, release)
- [ ] Test version calculation: `dotnet-gitversion`
- [ ] Document versioning workflow

**Duration:** 1 hour

---

#### Step 6: CI/CD Setup (Optional for Phase 2)

**Tasks:**
- [ ] Create `.github/workflows/nuget-publish.yml`
- [ ] Add NuGet API key to GitHub Secrets
- [ ] Test with manual workflow dispatch
- [ ] Configure automatic publish on tag push

**Duration:** 2-3 hours

**Note:** Can be deferred to Phase 5 (Documentation & CI/CD)

---

## Testing Strategy

### Local Testing Checklist

Before publishing packages to NuGet.org:

- [ ] **Build Success** - `dotnet build Hazina.sln --configuration Release`
- [ ] **Pack Success** - `.\scripts\pack-local.ps1` completes without errors
- [ ] **Metadata Validation** - All packages have description, tags, authors
- [ ] **Dependency Resolution** - No circular dependencies
- [ ] **Multi-Targeting** - Packages target net8.0, net9.0, net10.0
- [ ] **Symbol Packages** - `.snupkg` files generated for debugging
- [ ] **README Inclusion** - Each package includes README.md
- [ ] **Consumer Test** - Install meta-package in sample project and run
- [ ] **Version Consistency** - Related packages have compatible versions

### Sample Consumer Project Test

**Create test project:**
```bash
mkdir C:\Temp\HazinaTest
cd C:\Temp\HazinaTest
dotnet new console
dotnet add package Hazina --source Local
```

**Test code:**
```csharp
using Hazina.LLMs;
using Hazina.AI;

var client = new OpenAIClient("your-api-key");
var response = await client.CompleteAsync("Hello, world!");
Console.WriteLine(response);
```

**Expected:** Compiles and runs without errors.

---

## Package Size Optimization

### Size Targets

- **Core Foundation** - < 100 KB each (lightweight abstractions)
- **LLM Providers** - < 500 KB each (thin wrappers)
- **AI Core** - < 1 MB each (complex logic)
- **Tools Services** - < 500 KB each (single responsibility)
- **Meta-Packages** - 0 KB (dependencies only)

### Optimization Techniques

1. **Exclude Unnecessary Files**
   ```xml
   <ItemGroup>
     <None Remove="**\*.md;**\*.txt" />
   </ItemGroup>
   ```

2. **Trim Dependencies**
   ```xml
   <PackageReference Include="HeavyLib" Version="1.0.0" PrivateAssets="all" />
   ```

3. **Satellite Assemblies** - Split large packages into sub-packages

4. **Source-Only Packages** - For code generation or analyzers
   ```xml
   <IncludeBuildOutput>false</IncludeBuildOutput>
   <IncludeContentInPack>true</IncludeContentInPack>
   ```

---

## Documentation & Discoverability

### NuGet.org Package Page

**Optimizations:**
- ✅ Clear, concise description (280 chars max for search results)
- ✅ Relevant tags (10-15 tags for SEO)
- ✅ Package icon (128x128 PNG, recognizable)
- ✅ README.md (rendered on package page)
- ✅ Project URL (links to docs site)
- ✅ Release notes (document breaking changes)

### Tag Strategy

**Core Tags** (all packages):
```
hazina, ai, llm, dotnet, csharp
```

**Category Tags:**
```
LLM Providers: openai, anthropic, claude, gpt, gemini
AI Core: rag, agents, reasoning, neurochain, guardrails
Tools: websearch, scraping, automation, database, social
Infrastructure: auth, security, observability, logging
```

### Search Optimization

**Package Name:** `Hazina.[Feature]` - Always start with "Hazina" for brand recognition

**Description Format:**
```
[What it does] for Hazina AI Framework - [Key features, benefits, use cases]
```

**Example:**
```
Retrieval-Augmented Generation (RAG) for Hazina AI Framework - Semantic search, multi-layer validation, context optimization, production-ready
```

---

## Maintenance & Support

### Package Lifecycle

1. **Preview** (0.x.x) - Experimental, breaking changes expected
2. **Stable** (1.x.x) - Production-ready, semantic versioning
3. **LTS** (2.x.x+) - Long-term support, critical fixes only
4. **Deprecated** (marked in README) - Use alternative package

### Breaking Change Policy

**Major Version Bump Required For:**
- Removing public APIs
- Changing method signatures
- Renaming classes/namespaces
- Changing default behavior
- Dropping target framework support

**Minor Version Bump Allowed For:**
- Adding new APIs
- New optional parameters (with defaults)
- New features (backward compatible)
- Performance improvements
- Bug fixes (non-breaking)

### Support Matrix

| .NET Version | Support Status | End of Support |
|--------------|---------------|----------------|
| .NET 10.0 | ✅ Active | Nov 2027 |
| .NET 9.0 | ✅ Active | May 2025 |
| .NET 8.0 | ✅ LTS | Nov 2026 |
| .NET 7.0 | ❌ Unsupported | May 2024 |

**Policy:** Support latest 3 .NET versions (current LTS + current + preview)

---

## Appendix A: Complete Package List (108 packages)

### Core Foundation (12)
1. Hazina.LLMs.Client
2. Hazina.LLMs.Classes
3. Hazina.LLMs.Helpers
4. Hazina.LLMs.Tools
5. Hazina.LLMClientTools
6. Hazina.Store.EmbeddingStore
7. Hazina.Store.DocumentStore
8. Hazina.Store.FactsStore
9. Hazina.Store.Sqlite
10. Hazina.Tools.Core
11. Hazina.Tools.Data
12. Hazina.Tools.Models

### LLM Providers (8)
13. Hazina.LLMs.OpenAI
14. Hazina.LLMs.Anthropic
15. Hazina.LLMs.Gemini
16. Hazina.LLMs.GoogleADK
17. Hazina.LLMs.Mistral
18. Hazina.LLMs.HuggingFace
19. Hazina.LLMs.Ollama
20. Hazina.LLMs.SemanticKernel

### AI Core (28)
21. Hazina.AI.Core
22. Hazina.AI.Orchestration
23. Hazina.AI.FluentAPI
24. Hazina.AI.Providers
25. Hazina.LLMs.Registry
26. Hazina.AI.RAG
27. Hazina.AI.ContextEngineering
28. Hazina.AI.Compression
29. Hazina.LongContext
30. Hazina.Neurochain.Core
31. Hazina.AI.FaultDetection
32. Hazina.CodeIntelligence
33. Hazina.AI.Agents
34. Hazina.AI.Workflows
35. Hazina.AI.Guardrails
36. Hazina.AgentFactory
37. Hazina.Generator
38. Hazina.DynamicAPI
39. Hazina.AI.Vision
40. Hazina.AI.Training
41. Hazina.AI.Inference
42. Hazina.AI.LocalLLM
43. Hazina.Brain
44. Hazina.AI.Learning
45. Hazina.Quality
46. Hazina.Evals
47. Hazina.AI.Routing
48. Hazina.AI.TaskPrediction

### Tools & Services (45)
49. Hazina.Tools.Common.Models
50. Hazina.Tools.Common.Infrastructure.AspNetCore
51. Hazina.Tools.Extensions
52. Hazina.Tools.TextExtraction
53. Hazina.Tools.AI.Agents
54. Hazina.Tools.ContextCompression
55. Hazina.Tools.Services
56. Hazina.Tools.Services.Embeddings
57. Hazina.Tools.Services.Store
58. Hazina.Tools.Services.FileOps
59. Hazina.Tools.Services.Web
60. Hazina.Tools.Services.WebSearch
61. WebSearch.Core
62. WebSearch.Infrastructure
63. WebSearch.Providers
64. WebSearch
65. Hazina.Tools.Services.Chat
66. Hazina.Tools.Services.Images
67. Hazina.Tools.Services.Database
68. Hazina.Tools.Services.Social
69. Hazina.Tools.Services.ContentRetrieval
70. Hazina.Tools.Services.DataGathering
71. Hazina.Tools.Services.Intake
72. Hazina.Tools.Services.Prompts
73. Hazina.Tools.Services.BigQuery
74. Hazina.Tools.Services.WordPress
75. Hazina.Tools.Services.GoogleDrive
76. Hazina.Tools.Services.PDOK
77. Hazina.Tools.CsAutofix
78. Hazina.Tools.UIAutomationBridge
79. Hazina.Tools.WorkTray
80. Hazina.Production.Monitoring
81. Hazina.Tools.Migration

### Infrastructure (23)
82. Hazina.Auth.Core
83. Hazina.Auth.Identity
84. Hazina.Security.Core
85. Hazina.Security.AspNetCore
86. Hazina.Observability.Core
87. Hazina.Observability.AspNetCore
88. Hazina.Observability.LLMLogs
89. Hazina.API.Generic
90. Hazina.Terminal.API
91. Hazina.Terminal.ChatAgent
92. Hazina.EventSourcing
93. Hazina.CodeGeneration.Core
94. Hazina.Enterprise.Core
95. Hazina.Indexing
96. Hazina.Core.Plugins
97. Hazina.ChatShared
98. Hazina.UI.SchemaComponents
99. Hazina.AgenticOrchestration
100. Hazina.TaskRunner
101. Hazina.TaskRunner.UI
102. Hazina.Agent.API
103. Hazina.Agents.Tools
104. Hazina.Agents.Coding
105. Hazina.AI.DecisionTracking
106. Hazina.AI.PromptManagement
107. Hazina.AI.Memory
108. Hazina.AI.CognitivePipeline

### Meta-Packages (6)
109. Hazina (main bundle)
110. Hazina.Core (minimal)
111. Hazina.AI.Complete
112. Hazina.Providers.All
113. Hazina.Tools.Complete
114. Hazina.Web

---

## Appendix B: Scripts Reference

### Pack-Local.ps1

**Purpose:** Pack all packages to local NuGet feed for testing

**Location:** `scripts/pack-local.ps1`

**Usage:**
```powershell
.\scripts\pack-local.ps1 [-Configuration Release] [-Version 1.2.3-local]
```

### Publish-NuGet.ps1

**Purpose:** Pack and publish to NuGet.org

**Location:** `scripts/publish-nuget.ps1`

**Usage:**
```powershell
.\scripts\publish-nuget.ps1 -ApiKey "pk_xxx" [-Version 1.2.3] [-DryRun]
```

### Audit-Package-Metadata.ps1

**Purpose:** Validate package metadata completeness

**Location:** `scripts/audit-package-metadata.ps1`

**Usage:**
```powershell
.\scripts\audit-package-metadata.ps1 [-Fix]
```

**Checks:**
- ✅ All .csproj have `<PackageId>`, `<Version>`, `<Description>`
- ✅ README.md exists in each project directory
- ✅ Tags are relevant and complete
- ✅ License expression is MIT
- ✅ Repository URL is correct

---

## Appendix C: Sample Meta-Package .csproj

### Hazina.csproj (Main Bundle)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
    <PackageId>Hazina</PackageId>
    <Version>1.0.0</Version>
    <Title>Hazina - Complete AI Framework</Title>
    <Description>Complete Hazina AI Framework - Production-ready LLM orchestration, RAG, agents, tools, and infrastructure. Includes OpenAI provider, security, observability, and essential tooling.</Description>
    <PackageTags>hazina;ai;llm;framework;openai;anthropic;rag;agents;production;complete</PackageTags>
    <Authors>Hazina Team</Authors>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://docs.hazina.dev</PackageProjectUrl>
    <RepositoryUrl>https://github.com/martiendejong/Hazina.git</RepositoryUrl>
    <PackageReadmeFile>README.md</PackageReadmeFile>

    <!-- Meta-package: No build output, only dependencies -->
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <SuppressDependenciesWhenPacking>false</SuppressDependenciesWhenPacking>
  </PropertyGroup>

  <ItemGroup>
    <!-- Core Foundation -->
    <PackageReference Include="Hazina.LLMs.Client" Version="2.0.0" />
    <PackageReference Include="Hazina.LLMs.Classes" Version="2.0.0" />
    <PackageReference Include="Hazina.LLMs.Helpers" Version="1.0.0" />
    <PackageReference Include="Hazina.Store.EmbeddingStore" Version="1.0.0" />
    <PackageReference Include="Hazina.Store.DocumentStore" Version="1.0.0" />

    <!-- AI Core -->
    <PackageReference Include="Hazina.AI.Providers" Version="1.0.0" />
    <PackageReference Include="Hazina.AI.Orchestration" Version="1.0.0" />
    <PackageReference Include="Hazina.AI.FluentAPI" Version="1.0.0" />
    <PackageReference Include="Hazina.AI.RAG" Version="1.0.0" />
    <PackageReference Include="Hazina.AI.Agents" Version="1.0.0" />

    <!-- Default Provider -->
    <PackageReference Include="Hazina.LLMs.OpenAI" Version="2.0.0" />

    <!-- Essential Tools -->
    <PackageReference Include="Hazina.Tools.Services.FileOps" Version="1.0.0" />
    <PackageReference Include="Hazina.Tools.Services.Web" Version="1.0.0" />
    <PackageReference Include="Hazina.Tools.Services.Database" Version="1.0.0" />

    <!-- Security & Observability -->
    <PackageReference Include="Hazina.Security.Core" Version="1.0.0" />
    <PackageReference Include="Hazina.Observability.Core" Version="1.0.0" />
  </ItemGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

---

## Conclusion

This NuGet Package Strategy provides a comprehensive blueprint for publishing Hazina framework as modular, consumable packages while maintaining monorepo development efficiency.

### Key Takeaways

1. **108 packages** organized into 5 clear categories
2. **6 meta-packages** for convenient consumption patterns
3. **Independent versioning** with semantic versioning rules
4. **Local development** workflow with `C:\nuget-local\` feed
5. **CI/CD ready** with GitVersion and GitHub Actions
6. **Multi-targeting** .NET 8.0, 9.0, 10.0 (Phase 4 complete)

### Next Steps

- [ ] **Step 2:** Audit and standardize all 108 .csproj files
- [ ] **Step 3:** Create 6 meta-packages
- [ ] **Step 4:** Implement local feed automation
- [ ] **Step 5:** Configure GitVersion
- [ ] **Step 6:** (Optional) Set up CI/CD pipeline

**Estimated Total Implementation Time:** 8-10 hours

---

**Document Version:** 1.0
**Last Updated:** 2026-03-19
**Author:** Hazina Team
**Review Status:** Ready for implementation
