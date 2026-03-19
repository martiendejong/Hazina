# Hazina Framework - Changelog

All notable changes to the Hazina Framework are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
Versioning follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased] - develop branch

### Added
- Embedding store batch APIs, compaction service, and atomic write support
- Tool provider system with built-in providers, guardrails, tool sets, and mock support
- `ICapabilityProvider` interface implemented across all LLM client wrappers

### Changed
- Async embeddings initialization now uses lazy loading pattern

### Fixed
- Orphaned chunk mappings removed; parameter types aligned in embedding store
- Message roles clarified: User for inputs, Assistant for responses

---

## [2.6.x] - 2026 Q1 (develop)

### Added
- **LLM Provider Improvements** — Extended provider coverage with capability detection
- **Tool Provider Registry** — `IToolProvider`, `BuiltInToolProvider`, `MockToolProvider`
- **Tool Sets** — `ToolSet`, `ToolSetManager`, `StandardToolSets` for grouped tool management
- **Guardrails** — `IToolValidator`, `CompositeToolValidator`, `ToolGuardrails`
- **SemanticKernel upgrade** — Updated to 1.73.0; OpenAI SDK to 2.8.0
- **Phase 5 documentation** — `MODULE_GUIDE.md`, example projects, XML doc coverage

### Changed
- .NET versions standardized across all Hazina packages (PR #247)
- Legacy projects (`Hazina.Core`, `Hazina.Data`, `Hazina.AI.OpenCode`) marked deprecated

---

## [2.5.0] - 2025 Q4

**Theme:** JWT Authentication + WebSearch integration

### Added
- **JWT Authentication** for Hazina Orchestration service
- **WebSearch library** — Multi-target `Hazina.Tools.Services.Web` with Bing integration
- **Terminal Chat Agent** — `Hazina.Terminal.ChatAgent` with LLM integration
- **NuGet publishing infrastructure** — Simplified v2 workflow via `publish-all.ps1`

### Migration Guide
See [docs/migrations/v2.4-to-v2.5.md](../migrations/v2.4-to-v2.5.md)

---

## [2.4.3] - 2025 Q3

**Theme:** MSI Installer + Deployment

### Added
- User-folder MSI installer as the permanent default deployment method
- Verification document for installer correctness

### Fixed
- User-folder MSI installer Windows Service hosting
- Symbol package publishing errors (`--no-symbols` flag)
- NuGet package validation and push pipeline

---

## [2.4.2] - 2025 Q3

### Fixed
- NuGet packaging: excluded non-library projects from package generation
- Removed `PackageIcon` requirement (icon.png placeholder added to roadmap)
- Cleaned `nupkgs` directory and corrected PowerShell package validation syntax

---

## [2.0.0] - 2025

**Theme:** Architecture Consolidation + Breaking Changes

### Breaking Changes

| Area | Change | Migration |
|------|--------|-----------|
| Configuration | Constructor params → Object initializers | [v1-to-v2.md](../migrations/v1-to-v2.md) |
| Namespaces | `Hazina.LLMs` → provider-specific namespaces | [v1-to-v2.md](../migrations/v1-to-v2.md) |
| Method names | `GenerateTextAsync` → `GenerateAsync` | [v1-to-v2.md](../migrations/v1-to-v2.md) |
| Parameter order | `prompt, model` → `model, prompt` + CancellationToken | [v1-to-v2.md](../migrations/v1-to-v2.md) |

### Added
- **`HazinaConfigBase`** — Shared base class for all provider configs (~400 LOC saved)
- **`HazinaServiceBase`** — Base class for service implementations (~200 LOC saved)
- **`LLMProviderBase`** — Base class for LLM provider wrappers (~150 LOC saved)
- **Context Compression** — Up to 87% token reduction (`Hazina.AI.Compression`)
- **Google Drive Integration** — Document store backed by Google Drive
- **3-Layer Tool Agent Architecture** — Token-optimized orchestration via Ollama
- **Dynamic API Client** — Agents can call any API without pre-configuration
- **Token Usage Tracking** — All LLM calls return `LLMResponse<T>` with cost info
- **Code Quality Analyzers** — SonarAnalyzer, StyleCop, Meziantou added to `Directory.Build.props`

### Changed
- Provider classes reorganized into dedicated namespaces (`Hazina.LLMs.OpenAI`, `.Anthropic`, etc.)
- Configuration loading unified — all configs support `Config.Load()` from appsettings.json

### Removed
- Old constructor overloads on provider config classes
- Monolithic `Hazina.LLMs` namespace exports for provider-specific classes

### Total Reduction
~750 LOC eliminated through shared base class inheritance

---

## [1.x] - Legacy

**Status:** No longer maintained. Upgrade path: [v1-to-v2.md](../migrations/v1-to-v2.md)

Key differences from v2.0:

| Aspect | v1.x | v2.0+ |
|--------|------|--------|
| Config Pattern | Constructor params | Object initializers |
| Namespaces | Monolithic `Hazina.LLMs` | Provider-specific |
| Method Names | `GenerateTextAsync` | `GenerateAsync` |
| Base Classes | None | `HazinaConfigBase`, `HazinaServiceBase` |
| Token Tracking | None | Built-in `LLMResponse<T>` |
| Code Deduplication | None | ~750 LOC reduced |

---

## Links

- [Migration Guides](../migrations/)
- [Release Notes](RELEASE_NOTES.md)
- [API Changelog](../API_CHANGELOG.md)
- [GitHub Releases](https://github.com/martiendejong/Hazina/releases)
