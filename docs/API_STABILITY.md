# API Stability Policy

## Overview

Hazina is committed to providing stable, reliable APIs for developers building AI-powered applications. This document outlines our API stability guarantees, versioning strategy, and policies for handling breaking changes.

## Semantic Versioning

Hazina follows [Semantic Versioning 2.0.0](https://semver.org/) for all published packages:

- **MAJOR** version: Incompatible API changes
- **MINOR** version: Backward-compatible functionality additions
- **PATCH** version: Backward-compatible bug fixes

Given a version number `MAJOR.MINOR.PATCH`, we increment:
1. MAJOR when making incompatible API changes
2. MINOR when adding functionality in a backward-compatible manner
3. PATCH when making backward-compatible bug fixes

## API Stability Tiers

### Stable APIs

**Guarantee**: No breaking changes without MAJOR version increment and 12-month deprecation notice.

**Stable Core Assemblies**:
- `Hazina.LLMs.Client` - Core LLM client interfaces
- `Hazina.LLMs.Classes` - Common data models and types
- `Hazina.Store.DocumentStore` - Document storage interfaces
- `Hazina.Store.EmbeddingStore` - Vector storage interfaces
- `Hazina.AgentFactory` - Agent creation and lifecycle management

**Characteristics**:
- Public APIs are frozen except for additions
- Breaking changes require MAJOR version bump
- Deprecated APIs supported for minimum 12 months
- Comprehensive documentation and examples

### Stable with Extensions

**Guarantee**: Core functionality stable; new features may be added in MINOR versions.

**Assemblies**:
- `Hazina.AI.Orchestration` - Agent coordination
- `Hazina.AI.Memory` - Memory management systems
- `Hazina.AI.Providers` - LLM provider abstractions
- `Hazina.LLMs.Registry` - Provider discovery and registration

**Characteristics**:
- Core interfaces and base classes are stable
- New optional features added via MINOR versions
- Extension points may be added without breaking changes
- Backward compatibility maintained

### Experimental APIs

**Guarantee**: May change at any time. Marked with `[Experimental]` attribute.

**Example Experimental Assemblies**:
- `Hazina.AI.CognitivePipeline` - [EXPERIMENTAL: HAZ001] Multi-stage reasoning
- `Hazina.AI.ContextEngineering` - [EXPERIMENTAL: HAZ002] Advanced context optimization
- `Hazina.AI.FaultDetection` - [EXPERIMENTAL: HAZ003] Autonomous error detection
- `Hazina.AI.Learning` - [EXPERIMENTAL: HAZ004] Self-improvement capabilities
- `Hazina.AI.TaskPrediction` - [EXPERIMENTAL: HAZ005] Predictive task routing

**Characteristics**:
- Marked with `[Experimental("HAZXXX")]` attribute
- API surface may change in MINOR or PATCH versions
- May be promoted to stable or removed entirely
- Used for innovation and rapid iteration
- Documentation clearly states experimental status

### Internal APIs

**Guarantee**: No stability guarantees. Subject to change without notice.

**Characteristics**:
- Marked `internal` or in `*.Internal` namespaces
- Not included in public API documentation
- Used for implementation details only

## Breaking Change Policy

### What Constitutes a Breaking Change

Breaking changes include:
- Removing or renaming public types, members, or parameters
- Changing method signatures (parameters, return types)
- Changing behavior in ways that break existing usage
- Removing or changing the meaning of configuration options
- Changing serialization formats without migration path

### Deprecation Process

When a breaking change is necessary:

1. **Announcement** (T+0): Public announcement via:
   - Release notes
   - GitHub discussions
   - Documentation updates

2. **Deprecation** (T+0 to T+12 months):
   - Mark API with `[Obsolete("Use NewApi instead", false)]`
   - Provide migration guide in documentation
   - New API available alongside deprecated API

3. **Breaking Change** (T+12 months):
   - Remove deprecated API in next MAJOR version
   - Update `[Obsolete]` to error (`true`) in one MINOR version before removal
   - Comprehensive migration guide in MAJOR version release notes

### Example Deprecation Timeline

```csharp
// Version 1.5.0 - Original API
public interface ILlmClient
{
    Task<string> GenerateAsync(string prompt);
}

// Version 1.6.0 - New API introduced, old API deprecated
public interface ILlmClient
{
    [Obsolete("Use GenerateAsync(LlmRequest) instead. This method will be removed in v2.0.0", false)]
    Task<string> GenerateAsync(string prompt);

    Task<LlmResponse> GenerateAsync(LlmRequest request); // New API
}

// Version 1.11.0 - Final warning (1 minor version before v2.0.0)
public interface ILlmClient
{
    [Obsolete("Use GenerateAsync(LlmRequest) instead. This method will be removed in v2.0.0", true)]
    Task<string> GenerateAsync(string prompt);

    Task<LlmResponse> GenerateAsync(LlmRequest request);
}

// Version 2.0.0 - Breaking change
public interface ILlmClient
{
    Task<LlmResponse> GenerateAsync(LlmRequest request);
}
```

## Experimental API Policy

### Marking Experimental APIs

All experimental APIs MUST be marked with the `[Experimental]` attribute:

```csharp
[Experimental("HAZ001")]
public class CognitivePipelineBuilder
{
    // Experimental API surface
}
```

### Experimental API Codes

| Code | Feature Area | Assembly |
|------|-------------|----------|
| HAZ001 | Cognitive Pipeline | Hazina.AI.CognitivePipeline |
| HAZ002 | Context Engineering | Hazina.AI.ContextEngineering |
| HAZ003 | Fault Detection | Hazina.AI.FaultDetection |
| HAZ004 | Self-Learning | Hazina.AI.Learning |
| HAZ005 | Task Prediction | Hazina.AI.TaskPrediction |
| HAZ006 | Advanced RAG | Hazina.AI.RAG (advanced features) |
| HAZ007 | Local LLM Integration | Hazina.AI.LocalLLM |
| HAZ008 | Decision Tracking | Hazina.AI.DecisionTracking |
| HAZ009 | Compression | Hazina.AI.Compression |
| HAZ010 | Guardrails | Hazina.AI.Guardrails |

### Promotion to Stable

Experimental APIs may be promoted to stable when:
1. API has been in use for 6+ months
2. No breaking changes needed for 3+ consecutive MINOR versions
3. Comprehensive test coverage (>80%)
4. Production usage validation
5. Complete documentation

### Removal of Experimental APIs

Experimental APIs may be removed with:
- One MINOR version notice period
- Clear migration path or explanation
- Documentation of alternatives

## API Compatibility Testing

### Automated Compatibility Checks

We use the following tools to ensure API compatibility:

1. **Microsoft.CodeAnalysis.PublicApiAnalyzers**
   - Tracks public API surface in `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`
   - Prevents accidental API changes
   - Enforces explicit API reviews

2. **ApiCompat** (Microsoft.DotNet.ApiCompat)
   - Validates binary compatibility between versions
   - Detects breaking changes in assembly signatures
   - Runs in CI/CD pipeline

### CI/CD Integration

Every pull request:
1. Validates public API changes via PublicApiAnalyzers
2. Runs ApiCompat against previous MINOR version
3. Requires explicit approval for any public API additions
4. Blocks merging if breaking changes detected (without MAJOR version bump)

## Version Support Policy

### Long-Term Support (LTS)

- LTS versions supported for 2 years from release
- Security updates and critical bug fixes only
- No new features in LTS branches
- LTS versions: Every 4th MAJOR version (4.x, 8.x, 12.x, etc.)

### Current Versions

- Active development: Latest MAJOR.MINOR
- Security updates: Latest MAJOR.MINOR and current LTS
- Bug fixes: Latest MINOR within current MAJOR

### End of Life

- Announced 6 months before EOL
- No updates after EOL date
- Migration guides provided for upgrade path

## API Design Principles

### Consistency

- Follow .NET naming conventions
- Use consistent parameter ordering
- Prefer async methods for I/O operations
- Use standard .NET patterns (IDisposable, IAsyncEnumerable, etc.)

### Extensibility

- Design for inheritance and composition
- Provide extension points via interfaces
- Use dependency injection for loose coupling
- Avoid sealed classes unless necessary for security

### Backward Compatibility

- Add parameters as optional when possible
- Use method overloads instead of changing signatures
- Provide default implementations for new interface members (C# 8+)
- Version configuration schemas explicitly

## Feedback and Questions

For questions about API stability:
- Open a GitHub Discussion: https://github.com/martiendejong/Hazina/discussions
- File an issue: https://github.com/martiendejong/Hazina/issues
- Review release notes for upcoming changes

## Version History

| Date | Version | Changes |
|------|---------|---------|
| 2026-03-19 | 1.0 | Initial API stability policy |

---

**Last Updated**: 2026-03-19
**Applies to**: Hazina v0.3.0+
**Status**: Active
