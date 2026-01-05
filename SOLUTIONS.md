# Hazina Solution Files Guide

This repository uses multiple focused solution files to improve developer experience and reduce cognitive load.

## 🚀 Quick Start

**New to Hazina?** Start here:
```bash
# Open the QuickStart solution with top 10 essential packages
code Hazina.QuickStart.sln
# or
rider Hazina.QuickStart.sln
```

## 📂 Available Solutions

### Hazina.QuickStart.sln
**Best for**: Getting started, learning Hazina, simple integrations

**Contains** (10 projects):
- `Hazina.AI.FluentAPI` - Developer-first API
- `Hazina.AI.Providers` - Multi-provider abstraction
- `Hazina.Neurochain.Core` - Multi-layer reasoning
- `Hazina.AI.RAG` - RAG engine
- `Hazina.AI.Agents` - Agentic workflows
- `Hazina.LLMs.OpenAI` - OpenAI provider
- `Hazina.LLMs.Anthropic` - Anthropic provider
- `Hazina.Store.EmbeddingStore` - Embeddings storage
- `Hazina.Store.DocumentStore` - Document storage
- `Hazina.Production.Monitoring` - Production monitoring

**Use when**:
- You're new to Hazina
- Building a simple AI application
- Exploring capabilities

---

### Hazina.Core.sln
**Best for**: Core infrastructure work, LLM provider development

**Contains**: Foundation packages (LLMs, Storage, Security, Observability)

**Use when**:
- Working on LLM provider integrations
- Developing storage adapters
- Security or observability improvements

---

### Hazina.AI.sln
**Best for**: AI features development, advanced reasoning

**Contains**: All AI-related packages (Neurochain, RAG, Agents, Fault Detection, etc.)

**Use when**:
- Developing AI algorithms
- Working on multi-layer reasoning
- Building agentic workflows
- RAG improvements

---

### Hazina.Tools.sln
**Best for**: Tools and services development

**Contains**: Tools.Core, Tools.Services.*, Tool.AI.Agents, etc.

**Use when**:
- Building new services (BigQuery, Social, etc.)
- Extending tool capabilities
- Working on data processing pipelines

---

### Hazina.Apps.sln
**Best for**: Application development and demos

**Contains**: All apps (CLI, Desktop, Web, Demos)

**Use when**:
- Building end-user applications
- Creating demos
- Testing integrations
- Working on UI/UX

---

### Hazina.sln
**Best for**: Full solution builds, release preparation, comprehensive testing

**Contains**: All 62 projects

**Use when**:
- Preparing releases
- Running full test suites
- Validating cross-project changes
- Building all NuGet packages

---

## 🎯 Recommended Workflow

1. **Daily development**: Use focused solutions (QuickStart, AI, Tools, Apps)
   - Faster loading (2-3s vs 10-15s)
   - Less memory usage
   - Better IntelliSense performance
   - Reduced cognitive load

2. **Cross-cutting changes**: Use `Hazina.sln`
   - When changing shared interfaces
   - Before creating pull requests
   - For release builds

3. **Building packages**: Use `Hazina.sln`
   ```bash
   dotnet build Hazina.sln --configuration Release
   dotnet pack Hazina.sln --configuration Release
   ```

---

## 📊 Solution Statistics

| Solution | Projects | Load Time* | Memory** | Use Case |
|----------|----------|------------|----------|----------|
| **QuickStart** | 10 | ~2s | ~500MB | Getting started |
| **Core** | ~20 | ~4s | ~800MB | Infrastructure |
| **AI** | ~15 | ~3s | ~700MB | AI development |
| **Tools** | ~20 | ~4s | ~900MB | Services/tools |
| **Apps** | 14 | ~3s | ~600MB | Applications |
| **Full** | 62 | ~10s | ~2GB | Everything |

_*Approximate SSD load times in VS Code/Rider_
_**Approximate IDE memory usage_

---

## 🔍 Finding Projects

Not sure which solution contains the project you need?

```bash
# Search for a project in all solutions
grep -r "YourProject.csproj" *.sln

# List all projects in a solution
dotnet sln Hazina.QuickStart.sln list
```

---

## 🏗️ Creating Custom Solutions

Need a custom focused solution?

```bash
# Create new solution
dotnet new sln -n Hazina.Custom

# Add projects
dotnet sln Hazina.Custom.sln add path/to/Project.csproj

# Build your custom solution
dotnet build Hazina.Custom.sln
```

---

## 💡 Tips

1. **IntelliSense performance**: Focused solutions = faster IntelliSense
2. **Build performance**: Use incremental builds with focused solutions
3. **Git**: All solution files are tracked in git
4. **CI/CD**: GitHub Actions uses `Hazina.sln` for full builds

---

## 📚 Further Reading

- [CONTRIBUTING.md](CONTRIBUTING.md) - Contribution guidelines
- [ARCHITECTURE.md](docs/ARCHITECTURE.md) - Architecture overview
- [MONOREPO_QUICK_WINS_PLAN.md](MONOREPO_QUICK_WINS_PLAN.md) - Optimization details
