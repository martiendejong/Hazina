# Changelog

All notable changes to the Hazina Framework are documented here.

For detailed per-version notes, see [docs/releases/CHANGELOG.md](docs/releases/CHANGELOG.md).
For migration guides, see [docs/migrations/](docs/migrations/).

---

## [Unreleased]

- Embedding store batch APIs, compaction, and atomic writes
- Tool provider system with guardrails, tool sets, and mock support
- `ICapabilityProvider` across all LLM client wrappers

## [2.5.0]

- JWT authentication for Hazina Orchestration
- WebSearch library (`Hazina.Tools.Services.Web`)
- Terminal Chat Agent
- Simplified NuGet publishing pipeline

## [2.4.3]

- User-folder MSI installer as permanent default

## [2.4.2]

- NuGet packaging fixes

## [2.0.0]

**Breaking changes** — see [Migration Guide v1→v2](docs/migrations/v1-to-v2.md)

- Configuration refactor: object initializers replace constructor params
- Namespace reorganization: provider-specific namespaces
- `GenerateTextAsync` renamed to `GenerateAsync` with updated signature
- Token usage tracking via `LLMResponse<T>`
- Context Compression module (up to 87% token reduction)
- ~750 LOC removed through shared base classes

## [1.x]

Legacy. Upgrade path: [docs/migrations/v1-to-v2.md](docs/migrations/v1-to-v2.md)
