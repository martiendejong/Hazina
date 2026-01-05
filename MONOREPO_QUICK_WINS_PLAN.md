# Hazina Monorepo Quick Wins - Gedetailleerd Implementatieplan

**Datum**: 2026-01-05
**Status**: CONCEPT - Wacht op goedkeuring
**Doel**: Reduceer "bloat feeling" met 80% zonder repository te splitsen
**Tijdsinvestering**: ~1 week werk (gefaseerd)
**Risico**: Minimaal (geen breaking changes)

---

## 📊 Huidige Situatie - Bevindingen

### **Repository Grootte**
- **src/**: 168MB (source code) ✅ Klein en gezond
- **apps/**: 844MB (applicaties)
- **Tests/**: 4.0GB ⚠️ **PROBLEEM GEÏDENTIFICEERD**

### **Tests Directory Analyse**
```
Tests/ (4.0GB totaal)
├── Tools/              3.2GB  ⚠️
│   ├── Hazina.Tools.Services.Chat.Tests/         585MB
│   ├── Hazina.Tools.Services.Store.Tests/        532MB
│   ├── Hazina.Tools.Services.Embeddings.Tests/   532MB
│   ├── Hazina.Tools.TextExtraction.Tests/        530MB
│   └── Hazina.Tools.Services.FileOps.Tests/      530MB
└── Core/               739MB

Gevonden: bin/ en obj/ directories met build artifacts
├── Google.Ads.GoogleAds.dll        35MB × 2 (Debug + Release)
├── Spire.XLS.dll                   21MB × 2
├── Spire.Doc.dll                   20MB × 2
├── libpdfium.so (verschillende OS)  15MB × 10+
└── grpc_csharp_ext.*.dll           13MB × 8+
```

**Root cause**: Build artifacts (bin/obj) niet in git, maar lokaal gebuild
**Impact**: 4GB lokale disk space, maar NIET in repository
**Status**: .gitignore werkt correct ✅

---

## 🎯 Quick Wins Plan - 4 Fases

---

## **FASE 1: CLEANUP & HYGIENE** (Dag 1-2)

### **1.1 Verwijder Lokale Build Artifacts**
**Wat**: Cleanup 4GB aan bin/obj directories
**Waarom**: Disk space vrijmaken, snellere IDE indexing

**Commando's**:
```bash
# Veilig - alleen lokale build output
cd "C:\Projects\hazina"

# Optie A: Voorzichtig (met bevestiging)
git clean -xdn   # Preview wat verwijderd wordt
git clean -xdf   # Verwijder alles wat .gitignore negeert

# Optie B: Specifiek (alleen bin/obj)
find . -type d -name "bin" -exec rm -rf {} + 2>/dev/null
find . -type d -name "obj" -exec rm -rf {} + 2>/dev/null

# Windows PowerShell alternatief:
Get-ChildItem -Path . -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force
```

**Verwacht resultaat**:
- Tests directory: 4GB → ~200MB
- Totale repo size: ~5GB → ~1.2GB
- **80% disk space reductie** ✅

**Risico**: Geen (alleen build artifacts, gemakkelijk te herbouwen)
**Tijd**: 10 minuten

---

### **1.2 Archiveer Deprecated/Unused Projects**
**Wat**: Identificeer en archiveer ongebruikte packages
**Waarom**: Reduceer cognitive load, focus op active code

**Stappenplan**:

**Stap 1: Analyseer package usage** (na 1 maand live op NuGet)
```bash
# Check NuGet downloads per package
# Handmatig via: https://www.nuget.org/packages?q=Hazina
# Of via NuGet API:
curl "https://api.nuget.org/v3-flatcontainer/hazina.ai.providers/index.json"
```

**Criteria voor archivering**:
- <10 downloads na 1 maand
- Geen dependencies van andere packages
- Labeled als "experimental" of "deprecated"

**Stap 2: Maak archive structuur**
```bash
mkdir -p archive/2026-01-deprecated/{src,apps,tests}

# Verplaats deprecated packages
mv src/Core/Experimental/Hazina.Experimental.* archive/2026-01-deprecated/src/
mv apps/Demos/Hazina.Demo.Old* archive/2026-01-deprecated/apps/
```

**Stap 3: Update documentation**
```markdown
# In package README.md
⚠️ **DEPRECATED**: This package is archived and no longer maintained.
Use `Hazina.NewPackage` instead.

**Archive location**: `archive/2026-01-deprecated/`
```

**Verwacht resultaat**:
- Active projects: 76 → ~50-60 (schatting)
- **20-35% cognitive load reductie**

**Risico**: Minimaal (packages blijven beschikbaar in archive)
**Tijd**: 4-6 uur (analyse + verplaatsen + docs)

---

### **1.3 Optimaliseer .gitignore**
**Wat**: Verbeter .gitignore voor betere hygiene
**Waarom**: Voorkom toekomstige bloat

**Toevoegingen aan .gitignore**:
```gitignore
# Build artifacts (already present, but verify)
[Bb]in/
[Oo]bj/

# Test results
TestResults/
*.trx
*.coverage
*.coveragexml

# NuGet packages (local cache)
packages/
*.nupkg
*.snupkg

# Visual Studio cache/options
.vs/
.vscode/
*.user
*.suo

# Rider
.idea/

# ReSharper
_ReSharper*/

# NCrunch
*.ncrunch*
_NCrunch_*/

# Build cache
.msbuild/
artifacts/

# Local test data (if any)
**/test-data/
**/test-output/
```

**Commit**:
```bash
git add .gitignore
git commit -m "chore: Optimize .gitignore to prevent build artifacts

Added comprehensive ignore patterns for:
- Build outputs (bin/obj/artifacts)
- IDE files (.vs/.idea/.vscode)
- Test results (TestResults/coverage)
- Local caches

Prevents bloat from accumulating in working directory."
```

**Tijd**: 15 minuten

---

## **FASE 2: ORGANISATIE & VISIBILITY** (Dag 3-4)

### **2.1 Maak Focused Solution Files**
**Wat**: Meerdere .sln files voor verschillende focus areas
**Waarom**: Developers kunnen werken aan subset zonder overwelmd te worden

**Solution Structuur**:

```
Hazina/
├── Hazina.sln                    → ALLES (76 projecten) - Voor CI/CD
├── Hazina.Core.sln              → Foundation (15 projecten)
├── Hazina.AI.sln                → AI Features (12 projecten)
├── Hazina.Tools.sln             → Tools & Services (25 projecten)
├── Hazina.Apps.sln              → Applications (14 projecten)
├── Hazina.Security.sln          → Security & Observability (6 projecten)
└── Hazina.QuickStart.sln        → Top 10 belangrijkste packages
```

**Implementatie**:

**Stap 1: Genereer solution files**
```bash
# Core.sln - Foundation packages
dotnet new sln -n Hazina.Core
dotnet sln Hazina.Core.sln add src/Core/LLMs/**/*.csproj
dotnet sln Hazina.Core.sln add src/Core/Storage/**/*.csproj
dotnet sln Hazina.Core.sln add src/Tools/Foundation/Hazina.Tools.Core/*.csproj

# AI.sln - AI features
dotnet new sln -n Hazina.AI
dotnet sln Hazina.AI.sln add src/Core/AI/**/*.csproj

# Tools.sln - Tools & Services
dotnet new sln -n Hazina.Tools
dotnet sln Hazina.Tools.sln add src/Tools/**/*.csproj

# Apps.sln - Applications
dotnet new sln -n Hazina.Apps
dotnet sln Hazina.Apps.sln add apps/**/*.csproj

# Security.sln - Security & Observability
dotnet new sln -n Hazina.Security
dotnet sln Hazina.Security.sln add src/Core/Security/**/*.csproj
dotnet sln Hazina.Security.sln add src/Core/Observability/**/*.csproj

# QuickStart.sln - Top 10 packages (voor nieuwe developers)
dotnet new sln -n Hazina.QuickStart
dotnet sln Hazina.QuickStart.sln add src/Core/LLMs/Hazina.LLMs.Client/*.csproj
dotnet sln Hazina.QuickStart.sln add src/Core/AI/Hazina.AI.Providers/*.csproj
dotnet sln Hazina.QuickStart.sln add src/Core/AI/Hazina.AI.FluentAPI/*.csproj
dotnet sln Hazina.QuickStart.sln add src/Core/AI/Hazina.Neurochain.Core/*.csproj
dotnet sln Hazina.QuickStart.sln add src/Core/AI/Hazina.AI.RAG/*.csproj
dotnet sln Hazina.QuickStart.sln add src/Core/Storage/Hazina.Store.*/*.csproj
dotnet sln Hazina.QuickStart.sln add src/Tools/Foundation/Hazina.Tools.Core/*.csproj
```

**Stap 2: Documenteer welke solution voor welke use case**
```markdown
# CONTRIBUTING.md

## Welke Solution moet ik openen?

**Voor nieuwe developers** → `Hazina.QuickStart.sln`
  - Bevat top 10 belangrijkste packages
  - Snel te laden (~30 seconden)
  - Ideaal om te beginnen

**Voor AI feature development** → `Hazina.AI.sln`
  - Neurochain, RAG, Agents, Providers
  - Focus op AI capabilities

**Voor LLM provider integration** → `Hazina.Core.sln`
  - LLM clients, providers, storage
  - Foundation components

**Voor tool development** → `Hazina.Tools.sln`
  - Services, utilities, extensions

**Voor app development** → `Hazina.Apps.sln`
  - CLI tools, demos, visualizers

**Voor security/observability work** → `Hazina.Security.sln`
  - Security, logging, monitoring

**Voor CI/CD** → `Hazina.sln` (full solution)
  - Alle 76 projecten
  - Gebruikt door GitHub Actions
```

**Verwacht resultaat**:
- Developer laadt alleen relevante projecten
- Visual Studio snelheid: **3-5x sneller** (15-20 projecten vs 76)
- Cognitive load: **60-70% reductie**

**Risico**: Geen (backward compatible, Hazina.sln blijft werken)
**Tijd**: 2 uur (genereren + documenteren + testen)

---

### **2.2 Maak CODEOWNERS File**
**Wat**: Definieer clear ownership per module
**Waarom**: Duidelijkheid over wie verantwoordelijk is, GitHub auto-assigns reviewers

**Implementatie**:

**Bestand**: `.github/CODEOWNERS`
```
# Hazina Code Owners
# Format: path @owner1 @owner2

# Default owner for everything (fallback)
* @martiendejong

# === CORE INFRASTRUCTURE ===

# LLMs & Providers
/src/Core/LLMs/ @martiendejong @llm-team
/src/Core/LLMs.Providers/ @martiendejong @llm-team

# Storage
/src/Core/Storage/ @martiendejong @storage-team

# Security
/src/Core/Security/ @martiendejong @security-team

# Observability
/src/Core/Observability/ @martiendejong @observability-team

# === AI FEATURES ===

# AI Core
/src/Core/AI/Hazina.AI.Providers/ @martiendejong @ai-team
/src/Core/AI/Hazina.AI.FluentAPI/ @martiendejong @ai-team

# Neurochain (Multi-layer reasoning)
/src/Core/AI/Hazina.Neurochain.Core/ @martiendejong @ai-team @research-team

# RAG & Agents
/src/Core/AI/Hazina.AI.RAG/ @martiendejong @ai-team
/src/Core/AI/Hazina.AI.Agents/ @martiendejong @ai-team @agents-team

# Code Intelligence
/src/Core/AI/Hazina.CodeIntelligence/ @martiendejong @ai-team @code-quality-team

# === TOOLS & SERVICES ===

# Foundation Tools
/src/Tools/Foundation/ @martiendejong @tools-team

# Services (algemeen)
/src/Tools/Services/ @martiendejong @services-team

# Specific services
/src/Tools/Services/Hazina.Tools.Services.Social/ @martiendejong @social-team
/src/Tools/Services/Hazina.Tools.Services.BigQuery/ @martiendejong @data-team

# Production Monitoring
/src/Tools/Production/ @martiendejong @sre-team

# === APPLICATIONS ===

/apps/CLI/ @martiendejong @apps-team
/apps/Demos/ @martiendejong @demo-team
/apps/Visualizers/ @martiendejong @ui-team

# === TESTS ===

# Mirror ownership from src
/Tests/Core/AI/ @martiendejong @ai-team
/Tests/Core/LLMs/ @martiendejong @llm-team
/Tests/Tools/ @martiendejong @tools-team

# === BUILD & CI/CD ===

/.github/ @martiendejong @devops-team
/Dockerfile @martiendejong @devops-team
/docker-compose.yml @martiendejong @devops-team
/.github/workflows/ @martiendejong @ci-team

# === DOCUMENTATION ===

/docs/ @martiendejong @docs-team
/*.md @martiendejong @docs-team
/README.md @martiendejong
```

**Voordelen**:
- Auto-assign reviewers bij PR's
- Duidelijke contactpersonen per module
- GitHub UI toont owners in file browser

**Voor single-person team** (nu):
```
# Simplified CODEOWNERS (voor nu)
* @martiendejong

# Later uitbreiden als team groeit:
# /src/Core/AI/ @martiendejong @new-ai-developer
```

**Tijd**: 30 minuten

---

### **2.3 Update README met Hiërarchie**
**Wat**: Maak duidelijke component hierarchy
**Waarom**: Nieuwe developers weten meteen waar te beginnen

**Implementatie**:

**Update hoofdREADME.md**:
```markdown
# Hazina - AI Framework voor .NET

Hazina is een production-ready AI framework met multi-provider support,
multi-layer reasoning (Neurochain), en comprehensive security.

## 🎯 Waar te Beginnen?

### **90% van gebruikers heeft dit nodig** (Core Foundation)

| Package | Wat doet het? | Installatie |
|---------|--------------|-------------|
| **Hazina.AI.FluentAPI** | Simpele AI API, fluent syntax | `dotnet add package Hazina.AI.FluentAPI` |
| **Hazina.AI.Providers** | Multi-provider (OpenAI/Anthropic/etc) | `dotnet add package Hazina.AI.Providers` |
| **Hazina.LLMs.Client** | LLM client basis | `dotnet add package Hazina.LLMs.Client` |
| **Hazina.Tools.Core** | Utilities & helpers | `dotnet add package Hazina.Tools.Core` |

**Quick Start**:
```csharp
using Hazina.AI.FluentAPI;

QuickSetup.SetupAndConfigure(openAIKey, anthropicKey);
var result = await Hazina.AskSafeAsync("What is 2+2?");
```

---

### **🧠 Advanced AI Features** (Voor power users)

| Package | Wat doet het? | Voor wie? |
|---------|--------------|-----------|
| **Hazina.Neurochain.Core** | Multi-layer reasoning (95%+ betrouwbaar) | Production AI apps |
| **Hazina.AI.RAG** | Retrieval-augmented generation | Document Q&A |
| **Hazina.AI.Agents** | Autonomous agents met tools | Agent workflows |
| **Hazina.CodeIntelligence** | Code analysis & refactoring | AI code tools |
| **Hazina.AI.Memory** | Conversation memory | Chatbots |

---

### **🔌 LLM Providers** (Kies je AI backend)

| Package | Provider |
|---------|----------|
| **Hazina.LLMs.OpenAI** | OpenAI (GPT-4, GPT-3.5) |
| **Hazina.LLMs.Anthropic** | Anthropic (Claude) |
| **Hazina.LLMs.Gemini** | Google (Gemini) |
| **Hazina.LLMs.Mistral** | Mistral AI |
| **Hazina.LLMs.HuggingFace** | HuggingFace models |
| **Hazina.LLMs.Ollama** | Local models (Ollama) |

---

### **🔧 Tools & Services** (Optionele utilities)

**Foundation Tools**:
- `Hazina.Tools.Data` - Data management
- `Hazina.Tools.TextExtraction` - PDF/Doc extraction
- `Hazina.Tools.Extensions` - Extension methods

**Services** (voor specifieke use cases):
- `Hazina.Tools.Services.Embeddings` - Vector embeddings
- `Hazina.Tools.Services.FileOps` - File operations
- `Hazina.Tools.Services.Social` - Social media publishing
- `Hazina.Tools.Services.BigQuery` - BigQuery integration

**Production**:
- `Hazina.Security.Core` - API key encryption, validation
- `Hazina.Observability.Core` - Logging (Serilog + OpenTelemetry)
- `Hazina.Production.Monitoring` - Metrics, profiling

---

### **📱 Applications & Demos**

| App | Wat is het? |
|-----|-------------|
| **Hazina.App.ClaudeCode** | CLI tool voor AI development |
| **Hazina.Demo.Supabase** | Supabase integration demo |
| **Hazina.Demo.RAG** | RAG workflow voorbeeld |

---

## 📦 Alle 76 Packages

Voor volledige lijst: [PUBLICATION_SUMMARY.md](PUBLICATION_SUMMARY.md)

## 🏗️ Developer Guide

**Nieuwe developer?**
1. Clone repo: `git clone https://github.com/martiendejong/Hazina`
2. Open `Hazina.QuickStart.sln` (niet Hazina.sln!)
3. Build: `dotnet build`
4. Lees: [CONTRIBUTING.md](CONTRIBUTING.md)

**Werken aan specifieke module?**
- AI features → Open `Hazina.AI.sln`
- LLM providers → Open `Hazina.Core.sln`
- Tools → Open `Hazina.Tools.sln`
- Apps → Open `Hazina.Apps.sln`

Zie [CONTRIBUTING.md](CONTRIBUTING.md) voor details.

## 🏛️ Architectuur

```
Hazina/
├── Core/          → Foundation (LLMs, Storage, AI)
├── Tools/         → Utilities & Services
└── Apps/          → Applications

Dependencies:
Everything depends on Core → Core is stable
Tools depends on Core → Independent services
Apps use everything → End products
```

Zie [ARCHITECTURE.md](docs/ARCHITECTURE.md) voor diepgaande uitleg.
```

**Tijd**: 2-3 uur (schrijven + review)

---

### **2.4 Maak Component Dependency Graph**
**Wat**: Visualiseer dependencies tussen packages
**Waarom**: Maak complexiteit zichtbaar, spot circulaire dependencies

**Implementatie**:

**Stap 1: Installeer tooling**
```bash
dotnet tool install -g dotnet-depends
dotnet tool install -g NuGetPackageExplorer
```

**Stap 2: Genereer dependency graph**
```bash
cd "C:\Projects\hazina"

# Genereer voor hele solution
dotnet depends analyze Hazina.sln --format graphml --output hazina-dependencies.graphml

# Of per module:
dotnet depends analyze Hazina.AI.sln --format svg --output docs/diagrams/ai-dependencies.svg
```

**Stap 3: Visualiseer met online tool**
```
Upload hazina-dependencies.graphml naar:
- yEd (https://www.yworks.com/yed-live/)
- Graphviz online
- Draw.io
```

**Stap 4: Document in README**
```markdown
## 📊 Dependency Graph

![Hazina Dependencies](docs/diagrams/hazina-dependencies.svg)

**Key insights**:
- ✅ No circular dependencies
- ✅ Clear layering (Core → Tools → Apps)
- ⚠️ High coupling in X (consider refactoring)
```

**Verwacht resultaat**:
- Visueel overzicht van alle dependencies
- Spot problematische coupling
- Beter begrip van architectuur

**Tijd**: 1-2 uur (setup + genereren + documenteren)

---

## **FASE 3: BUILD PERFORMANCE** (Dag 5)

### **3.1 Enable Incremental Builds**
**Wat**: MSBuild optimalisaties voor snellere builds
**Waarom**: Rebuild time 2-3 min → <30 sec (voor kleine changes)

**Implementatie**:

**Bestand**: `Directory.Build.props` (root van repo)
```xml
<Project>
  <!-- Shared properties voor ALLE projecten -->
  <PropertyGroup>
    <!-- .NET versie -->
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>

    <!-- Build optimalisatie -->
    <IncrementalBuild>true</IncrementalBuild>
    <BuildInParallel>true</BuildInParallel>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>

    <!-- NuGet optimalisatie -->
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <RestoreLockedMode Condition="'$(CI)' == 'true'">true</RestoreLockedMode>

    <!-- Output optimalisatie -->
    <ProduceReferenceAssembly>true</ProduceReferenceAssembly>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>

    <!-- Deterministic builds (betere caching) -->
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
  </PropertyGroup>

  <!-- Package metadata (voor NuGet packages) -->
  <PropertyGroup>
    <Authors>Martien de Jong</Authors>
    <Company>Hazina</Company>
    <Copyright>Copyright © 2026 Martien de Jong</Copyright>
    <PackageProjectUrl>https://github.com/martiendejong/Hazina</PackageProjectUrl>
    <RepositoryUrl>https://github.com/martiendejong/Hazina</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageTags>AI;LLM;OpenAI;Anthropic;Neurochain;RAG</PackageTags>
  </PropertyGroup>

  <!-- Analyzer configuration -->
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="9.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

**Bestand**: `Directory.Build.targets` (optional, voor advanced)
```xml
<Project>
  <!-- Custom build targets -->

  <!-- Skip tests in fast build mode -->
  <Target Name="SkipTestsInFastMode"
          BeforeTargets="Build"
          Condition="'$(FastBuild)' == 'true'">
    <Message Importance="high" Text="Fast build mode: Skipping tests" />
  </Target>
</Project>
```

**Usage**:
```bash
# Normal build (met alle checks)
dotnet build

# Fast build (skip analyzers)
dotnet build -p:RunAnalyzers=false

# Fast build (skip tests) - voor quick iteration
dotnet build -p:FastBuild=true

# CI build (met alle checks + deterministic)
dotnet build -p:CI=true
```

**Verwacht resultaat**:
- **Incremental build**: 2-3 min → 10-30 sec
- **Full rebuild**: 2-3 min → 1.5-2 min (parallel builds)
- **NuGet restore**: Cached met lock files

**Tijd**: 1 uur (setup + test + document)

---

### **3.2 Setup Build Caching**
**Wat**: Cache build artifacts voor snellere rebuilds
**Waarom**: Hergebruik van eerder gecompileerde code

**Implementatie**:

**Lokaal (developer machines)**:
```bash
# MSBuild cache locatie configureren
# In Environment Variables of .bashrc/.zshrc
export NUGET_PACKAGES="$HOME/.nuget/packages"
export DOTNET_CLI_HOME="$HOME/.dotnet"

# Shared build output (optioneel)
# Voor teams die shared build cache willen
export DOTNET_SHARED_COMPILATION_PATH="/path/to/shared/cache"
```

**CI/CD (GitHub Actions)** - Al geconfigureerd, maar verifieer:
```yaml
# .github/workflows/build-and-test.yml
- name: Cache NuGet packages
  uses: actions/cache@v3
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/packages.lock.json') }}
    restore-keys: |
      ${{ runner.os }}-nuget-

- name: Cache build outputs
  uses: actions/cache@v3
  with:
    path: |
      **/bin
      **/obj
    key: ${{ runner.os }}-build-${{ github.sha }}
    restore-keys: |
      ${{ runner.os }}-build-
```

**Tijd**: 30 minuten (verifiëren + documenteren)

---

## **FASE 4: DOCUMENTATION & ONBOARDING** (Dag 6-7)

### **4.1 Maak CONTRIBUTING.md**
**Wat**: Developer onboarding guide
**Waarom**: Nieuwe developers weten hoe te starten

**Implementatie**:

**Bestand**: `CONTRIBUTING.md`
```markdown
# Contributing to Hazina

Bedankt voor je interesse in Hazina! Deze guide helpt je om snel te starten.

## 🚀 Quick Start (5 minuten)

### Stap 1: Clone Repository
```bash
git clone https://github.com/martiendejong/Hazina
cd Hazina
```

### Stap 2: Open Juiste Solution
**Belangrijk**: Open NIET `Hazina.sln` (te groot)!

Voor jouw use case:
- **Nieuwe developer?** → `Hazina.QuickStart.sln` (10 projecten)
- **AI features** → `Hazina.AI.sln` (12 projecten)
- **LLM providers** → `Hazina.Core.sln` (15 projecten)
- **Tools/Services** → `Hazina.Tools.sln` (25 projecten)
- **Apps** → `Hazina.Apps.sln` (14 projecten)

### Stap 3: Build
```bash
dotnet restore
dotnet build
dotnet test
```

**Verwachte tijd**: 2-3 minuten (eerste keer), <1 min daarna

---

## 📁 Repository Structuur

```
Hazina/
├── src/
│   ├── Core/          → Foundation (LLMs, AI, Storage, Security)
│   └── Tools/         → Services & Utilities
├── apps/              → Applications (CLI, Demos)
├── Tests/             → Unit & Integration tests
├── docs/              → Documentation
└── archive/           → Deprecated code

Solutions:
├── Hazina.sln             → ALLES (alleen voor CI/CD)
├── Hazina.QuickStart.sln  → Top 10 packages (start hier!)
├── Hazina.AI.sln          → AI features
├── Hazina.Core.sln        → Core/LLMs
└── Hazina.Tools.sln       → Tools & Services
```

**Welke solution?**
1. **Quick start** (nieuwe dev) → `Hazina.QuickStart.sln`
2. **Specific feature** → Zie hierboven
3. **CI/CD pipeline** → `Hazina.sln` (full)

---

## 🏗️ Development Workflow

### Maak een Branch
```bash
git checkout -b feature/my-awesome-feature
```

### Maak Changes
```bash
# Edit files
code src/Core/AI/Hazina.AI.NewFeature/

# Build incrementeel
dotnet build

# Run tests
dotnet test
```

### Commit
```bash
git add .
git commit -m "feat: Add awesome feature

- Implemented X
- Added tests for Y
- Updated docs

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

### Push & Create PR
```bash
git push origin feature/my-awesome-feature

# GitHub zal automatisch CODEOWNERS assignen voor review
```

---

## 📦 Nieuwe Package Toevoegen

### Stap 1: Maak Project
```bash
cd src/Core/AI
dotnet new classlib -n Hazina.AI.NewFeature
```

### Stap 2: Voeg toe aan Solution
```bash
dotnet sln Hazina.AI.sln add src/Core/AI/Hazina.AI.NewFeature/Hazina.AI.NewFeature.csproj
dotnet sln Hazina.sln add src/Core/AI/Hazina.AI.NewFeature/Hazina.AI.NewFeature.csproj
```

### Stap 3: Update .csproj (inherits from Directory.Build.props)
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Versie -->
    <Version>1.0.0</Version>

    <!-- Package info -->
    <PackageId>Hazina.AI.NewFeature</PackageId>
    <Description>Description of new feature</Description>

    <!-- Optional: Specifieke dependencies -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <!-- Dependencies -->
    <ProjectReference Include="../../LLMs/Hazina.LLMs.Client/Hazina.LLMs.Client.csproj" />
  </ItemGroup>
</Project>
```

### Stap 4: Maak Tests
```bash
cd ../../../Tests/Core/AI
dotnet new xunit -n Hazina.AI.NewFeature.Tests
dotnet sln ../../../Hazina.sln add Hazina.AI.NewFeature.Tests/Hazina.AI.NewFeature.Tests.csproj
```

---

## 🧪 Testing

### Run All Tests
```bash
dotnet test
```

### Run Specific Project Tests
```bash
dotnet test Tests/Core/AI/Hazina.AI.Providers.Tests/
```

### Run met Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

## 📊 Code Quality

### Analyzers
Alle projecten hebben .NET analyzers enabled (via Directory.Build.props).

Warnings fixen:
```bash
dotnet build /warnaserror
```

### Code Style
- Follow C# naming conventions
- Use `var` voor obvious types
- XML comments voor public APIs
- Async methods eindigen op `Async`

---

## 🔍 Debugging

### Visual Studio
1. Open solution (bijv. `Hazina.AI.sln`)
2. Set startup project (right-click project → Set as Startup Project)
3. F5 to debug

### Visual Studio Code
1. Install C# Dev Kit extension
2. Open folder: `code .`
3. F5 to debug (select project)

### Rider
1. Open solution
2. Run → Debug (Shift+F9)

---

## 📝 Documentation

### Code Comments
```csharp
/// <summary>
/// Does something awesome with AI
/// </summary>
/// <param name="input">The input data</param>
/// <returns>The awesome result</returns>
public async Task<string> DoAwesomeThingAsync(string input)
{
    // Implementation
}
```

### README per Package
Elk package heeft eigen README in package directory:
```markdown
# Hazina.AI.NewFeature

Short description.

## Installation
dotnet add package Hazina.AI.NewFeature

## Usage
[code example]
```

---

## 🚀 Publishing (Maintainers only)

### NuGet Packages
```bash
# Automatically via GitHub Actions on tag push
git tag v1.0.1
git push origin v1.0.1
```

### Manual Publish
```bash
dotnet pack Hazina.sln -c Release
dotnet nuget push nupkgs/*.nupkg --api-key $NUGET_API_KEY
```

---

## 📞 Need Help?

- **Questions?** → [GitHub Discussions](https://github.com/martiendejong/Hazina/discussions)
- **Bugs?** → [GitHub Issues](https://github.com/martiendejong/Hazina/issues)
- **Docs?** → [docs/](docs/)

Happy coding! 🎉
```

**Tijd**: 3-4 uur (schrijven + review + update)

---

### **4.2 Maak ARCHITECTURE.md**
**Wat**: High-level architectuur documentatie
**Waarom**: Developers begrijpen design decisions

**Implementatie**: (zie volgende sectie voor inhoud)
**Tijd**: 2-3 uur

---

### **4.3 Update Package README's**
**Wat**: Elke package heeft eigen README
**Waarom**: Developers weten wat package doet zonder hele repo te lezen

**Template per package**:
```markdown
# [Package Name]

One-line description.

## Installation
```bash
dotnet add package [PackageName]
```

## Quick Start
```csharp
// Minimal working example (5-10 lines)
```

## Features
- Feature 1
- Feature 2

## Documentation
See [main docs](../../docs/)

## Dependencies
- Package A
- Package B

## License
MIT
```

**Implementatie**:
```bash
# Script om README's te genereren voor packages die het nog niet hebben
for dir in src/Core/*/Hazina.*/ ; do
  if [ ! -f "$dir/README.md" ]; then
    echo "Generating README for $dir"
    # Template based on package name
  fi
done
```

**Tijd**: 4-6 uur (voor 76 packages, kan ge-automated worden)

---

## **SAMENVATTEND TIJDSCHEMA**

| Fase | Taken | Tijd | Prioriteit |
|------|-------|------|------------|
| **Fase 1** | Cleanup & Hygiene | 1-2 dagen | 🔥 Hoog |
| 1.1 | Verwijder build artifacts | 10 min | Critical |
| 1.2 | Archiveer deprecated packages | 4-6 uur | Hoog |
| 1.3 | Optimaliseer .gitignore | 15 min | Medium |
| **Fase 2** | Organisatie & Visibility | 2 dagen | 🔥 Hoog |
| 2.1 | Focused solution files | 2 uur | Critical |
| 2.2 | CODEOWNERS file | 30 min | Medium |
| 2.3 | Update README hiërarchie | 2-3 uur | Hoog |
| 2.4 | Dependency graphs | 1-2 uur | Medium |
| **Fase 3** | Build Performance | 1 dag | Medium |
| 3.1 | Incremental builds | 1 uur | Hoog |
| 3.2 | Build caching | 30 min | Medium |
| **Fase 4** | Documentation | 1-2 dagen | Medium |
| 4.1 | CONTRIBUTING.md | 3-4 uur | Hoog |
| 4.2 | ARCHITECTURE.md | 2-3 uur | Medium |
| 4.3 | Package README's | 4-6 uur | Low (kan later) |

**Totaal**: ~5-7 dagen werk (kan gefaseerd)

---

## **VERWACHTE RESULTATEN**

### **Metrics - Voor/Na**

| Metric | Voor | Na | Verbetering |
|--------|------|-----|-------------|
| **Disk usage** | 5GB | 1.2GB | -76% |
| **Active projects** | 76 | ~50-60 | -20-35% |
| **Visual Studio load** | 76 projecten | 10-25 | -67-87% |
| **Build time (incremental)** | 2-3 min | 10-30 sec | -83-95% |
| **Build time (full)** | 2-3 min | 1.5-2 min | -25-33% |
| **Developer onboarding** | Confused | Clear path | ∞% |
| **Cognitive load** | Overwhelmed | Focused | -80% |

### **Developer Experience**

**Voor**:
- ❌ Clone repo → 76 projecten → "where do I start?"
- ❌ Open Hazina.sln → Visual Studio traag
- ❌ Build changes → 3 minuten wachten
- ❌ Onduidelijk welke packages belangrijk zijn
- ❌ Geen duidelijke ownership

**Na**:
- ✅ Clone repo → Open Hazina.QuickStart.sln → 10 projecten
- ✅ Visual Studio snel (alleen relevante projecten)
- ✅ Build changes → <30 seconden (incremental)
- ✅ Duidelijke hiërarchie in README
- ✅ CODEOWNERS toont verantwoordelijken

**Impact**: 80%+ reductie in "bloat feeling" ✅

---

## **RISICO ANALYSE**

| Risico | Waarschijnlijkheid | Impact | Mitigatie |
|--------|-------------------|---------|-----------|
| Accidental data loss bij cleanup | Laag | Hoog | Git protect, preview eerst (`git clean -xdn`) |
| Breaking changes in dependencies | Laag | Medium | Geen - alleen docs/organisation changes |
| Developer confusion bij solutions | Medium | Laag | Duidelijke docs in CONTRIBUTING.md |
| Build cache issues | Laag | Laag | Cache is optioneel, kan disabled worden |
| Time investment te hoog | Medium | Laag | Kan gefaseerd (1-2 taken per week) |

**Overall Risk**: **LAAG** ✅

---

## **BESLISPUNTEN**

Voor implementatie, bevestiging nodig op:

### **1. Archivering Criteria**
**Vraag**: Welke packages archiveren?
**Opties**:
A. <10 downloads na 1 maand
B. <50 downloads na 1 maand
C. Handmatig selecteren op basis van usage
D. Nog niet archiveren, eerst data verzamelen

### **2. Solution Naming**
**Vraag**: Zijn solution namen OK?
- Hazina.QuickStart.sln
- Hazina.AI.sln
- Hazina.Core.sln
- Hazina.Tools.sln
- Hazina.Apps.sln

**Alternatief**:
- Hazina-QuickStart.sln (met dash)
- Of andere namen?

### **3. CODEOWNERS Teams**
**Vraag**: Nu single-owner (`* @martiendejong`) of alvast team placeholders?
**Impact**: Voor nu simpel houden, later uitbreiden

### **4. Package README's**
**Vraag**: Nu alle 76 README's maken, of incrementeel?
**Aanbeveling**: Incrementeel (top 20 eerst)

### **5. Fasering**
**Vraag**: Alles in 1 week, of gefaseerd over 1 maand?
**Aanbeveling**:
- Week 1: Fase 1 + 2.1 (cleanup + solutions)
- Week 2: Fase 2.2-2.4 (docs + graphs)
- Week 3: Fase 3 (build perf)
- Week 4: Fase 4 (remaining docs)

---

## **NEXT STEPS - Na Goedkeuring**

1. **Review dit plan** → Feedback/wijzigingen
2. **Beslispunten beantwoorden** → Criteria vaststellen
3. **Fase 1 starten** → Cleanup (laag risico)
4. **Itereren** → Feedback na elke fase

**Klaar om te starten zodra je goedkeuring geeft!** ✅

---

**Voor nu**: **NIETS UITGEVOERD** - wacht op jouw feedback! 😊
