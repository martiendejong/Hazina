# Hazina Framework - Release Notes

Curated release notes for major versions. For the full commit-level changelog see [CHANGELOG.md](CHANGELOG.md).

---

## v2.6 (Unreleased — develop)

**Theme:** Embedding Storage + Tool Provider System

### Highlights

**Embedding Storage Overhaul**
Batch indexing, compaction, and atomic writes land in `Hazina.Store.EmbeddingStore`. Large-scale RAG workflows can now index and maintain embedding stores without consistency issues.

**Tool Provider System**
A structured provider-registry pattern for tool management:
- `IToolProvider` / `BuiltInToolProvider` — register and expose tools as a unit
- `ToolSet` / `ToolSetManager` / `StandardToolSets` — group tools by capability
- `IToolValidator` / `CompositeToolValidator` — validate tool calls before execution
- `MockToolProvider` — drop-in test double for tool-heavy agents

**ICapabilityProvider Across LLM Clients**
Every LLM wrapper now declares its capabilities (streaming, function calling, vision, etc.) so the orchestrator can route intelligently.

### Packages Affected
- `Hazina.Store.EmbeddingStore`
- `Hazina.LLMs.Classes`
- `Hazina.AI.Guardrails`
- `Hazina.LLMs.Anthropic`, `Hazina.LLMs.OpenAI`, `Hazina.LLMs.Gemini`, `Hazina.LLMs.Mistral`, `Hazina.LLMs.HuggingFace`, `Hazina.LLMs.Ollama`, `Hazina.LLMs.SemanticKernel`

---

## v2.5.0

**Released:** 2025 Q4
**Theme:** JWT Authentication + WebSearch + NuGet Publishing

### Highlights

**JWT Authentication in Hazina Orchestration**
Production-grade JWT authentication now secures the Hazina Orchestration HTTP API. Token validation, claims-based authorization, and refresh-token support are included.

**WebSearch Library**
`Hazina.Tools.Services.Web` provides multi-target web search (Bing API) so agents can retrieve live web content during reasoning loops.

**Terminal Chat Agent**
`Hazina.Terminal.ChatAgent` enables local terminal-based LLM conversations with context management and configurable provider selection.

**NuGet Publishing Pipeline**
Simplified `publish-all.ps1` workflow. All library packages publish cleanly with symbol packages, source link, and deterministic builds configured in `Directory.Build.props`.

### Migration from v2.4.x
No breaking API changes. See [v2.4-to-v2.5.md](../migrations/v2.4-to-v2.5.md) for infrastructure changes.

---

## v2.4.3

**Released:** 2025 Q3
**Theme:** MSI Installer Stability

### Highlights

User-folder MSI installer adopted as the permanent default deployment. No Windows Service registration required — runs cleanly in user space, simplifying installs on corporate machines.

---

## v2.0.0

**Released:** 2025
**Theme:** Architecture Consolidation

### Highlights

**Configuration Refactor**
All provider configs (`OpenAIConfig`, `AnthropicConfig`, etc.) now inherit from `HazinaConfigBase`. The new pattern uses object initializers and supports automatic binding from `appsettings.json`.

```csharp
// v2.0
var config = new OpenAIConfig { ApiKey = "sk-...", Model = "gpt-4o-mini" };

// or load from appsettings.json automatically
var config = OpenAIConfig.Load();
```

**Namespace Reorganization**
Provider classes moved from the monolithic `Hazina.LLMs` namespace to dedicated namespaces:
- `Hazina.LLMs.OpenAI`
- `Hazina.LLMs.Anthropic`
- `Hazina.LLMs.Ollama`
- `Hazina.LLMs.Gemini`
- `Hazina.LLMs.Mistral`

**Token Usage Tracking**
Every LLM call now returns `LLMResponse<T>` containing `TokenUsageInfo` with input/output counts and dollar cost for OpenAI, Anthropic, and HuggingFace.

```csharp
var response = await agent.Generator.GetResponse("Hello", cancel);
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
```

**Context Compression**
`Hazina.AI.Compression` reduces LLM request token counts by up to 87% while preserving essential context — directly cuts inference costs for long-context applications.

**Code Deduplication**
~750 LOC removed by introducing `HazinaConfigBase`, `HazinaServiceBase`, and `LLMProviderBase` shared base classes.

### Breaking Changes
Full list: [API_CHANGELOG.md](../API_CHANGELOG.md)
Migration guide: [v1-to-v2.md](../migrations/v1-to-v2.md)

---

## Support

- Issues: https://github.com/martiendejong/Hazina/issues
- Discussions: https://github.com/martiendejong/Hazina/discussions
- NuGet: https://www.nuget.org/profiles/Hazina
