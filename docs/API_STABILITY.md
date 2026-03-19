# Hazina API Stability Guidelines

## Overview

This document defines the API stability policy for the Hazina AI Framework. It provides guidelines for maintaining backward compatibility and clearly communicating breaking changes to consumers.

## Versioning Policy

Hazina follows [Semantic Versioning 2.0.0](https://semver.org/):

- **MAJOR**: Incompatible API changes
- **MINOR**: Backward-compatible functionality additions
- **PATCH**: Backward-compatible bug fixes

## API Surface Classification

### Public API (Stable)

Public APIs are designed for external consumption and carry strong backward compatibility guarantees:

- **Interfaces**: Core abstractions like `IModelInference`, `ILLMProvider`, `IEmbeddingStore`
- **Models**: DTOs and data structures exposed in public interfaces
- **Extension Methods**: Public extension methods for core types
- **Configuration Classes**: Options and configuration objects

**Stability Guarantee**: No breaking changes within a major version.

### Internal API

Internal APIs are implementation details not intended for external consumption:

- **Implementation Classes**: Concrete implementations marked with `internal` modifier
- **Helper Classes**: Utility classes marked with `internal` modifier
- **Internal Dependencies**: Types only used within a single assembly

**Stability Guarantee**: No guarantee. May change at any time.

### Experimental API

Experimental APIs are marked with `[Experimental]` attribute and may change or be removed:

```csharp
[Experimental("HAZ001", UrlFormat = "https://aka.ms/hazina/warnings/{0}")]
public interface IExperimentalFeature { }
```

**Stability Guarantee**: May change or be removed in any release.

## Access Modifier Guidelines

### Use `internal` for:

1. **Implementation classes** that implement public interfaces
2. **Helper/utility classes** used only within the assembly
3. **Internal services** not meant for direct consumption
4. **Implementation details** of public APIs

### Use `public` for:

1. **Interfaces** defining contracts
2. **Abstract base classes** meant for inheritance
3. **DTOs and models** exposed in public APIs
4. **Extension methods** for public types
5. **Factory classes** for creating public types

### Use `sealed` for:

1. **Implementation classes** not designed for inheritance
2. **Configuration classes** to prevent modification
3. **Performance-critical paths** where virtual calls should be avoided

## Breaking Change Policy

### What Constitutes a Breaking Change

- Removing or renaming public types or members
- Changing method signatures (parameters, return types)
- Changing interface contracts
- Changing behavior in observable ways
- Changing assembly names or namespaces
- Changing nullability contracts (when nullable reference types enabled)

### What Is NOT a Breaking Change

- Adding new methods to interfaces with default implementations
- Adding new optional parameters with default values
- Making sealed classes unsealed (if they don't contain protected members)
- Making internal types public
- Improving exception messages
- Performance improvements
- Bug fixes that change behavior to match documentation

## InternalsVisibleTo Policy

### When to Use

Use `[InternalsVisibleTo]` for:

1. **Test assemblies**: `Hazina.PackageName.Tests`
2. **Tightly coupled packages**: Within the same subsystem only
3. **Migration scenarios**: Temporary during refactoring

### When NOT to Use

- To expose implementation details across subsystems
- As a substitute for proper public API design
- For convenience without architectural justification

### Declaration

Add to `AssemblyInfo.cs` or in the project file:

```csharp
[assembly: InternalsVisibleTo("Hazina.PackageName.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // For Moq
```

## Documentation Requirements

### All Public APIs Must Have

1. **XML Documentation Comments**
   ```csharp
   /// <summary>
   /// Brief description of the type or member.
   /// </summary>
   /// <param name="paramName">Description of the parameter.</param>
   /// <returns>Description of the return value.</returns>
   /// <exception cref="ExceptionType">When this exception is thrown.</exception>
   ```

2. **Usage Examples** in docs/samples/
3. **Remarks Section** for complex behavior
4. **See Also References** to related types

### Documentation Standards

- Keep summaries concise (1-2 sentences)
- Include code examples for non-trivial usage
- Document all parameters and return values
- List all exceptions that may be thrown
- Cross-reference related types and members

## Analyzer Configuration

### Nullable Reference Types

All projects have nullable reference types enabled:

```xml
<Nullable>enable</Nullable>
```

**Guidelines:**
- Use `?` for nullable reference types
- Use `!` null-forgiving operator only when you're certain
- Prefer `[NotNull]` and `[MaybeNull]` attributes for clarity

### Analyzers in Use

1. **Microsoft.CodeAnalysis.NetAnalyzers**
   - Enabled: All rules at latest level
   - Nullable warnings as errors (CS8600-CS8604)

2. **SonarAnalyzer.CSharp**
   - Cognitive complexity target: ≤15
   - Method length target: ≤30 lines

3. **StyleCop.Analyzers**
   - Documentation: Warning level for public APIs
   - Ordering: Suggestion level

4. **Meziantou.Analyzer**
   - Additional code quality and performance rules

### Suppression Policy

Only suppress analyzer warnings when:
1. The analyzer is incorrect (file a bug with analyzer project)
2. The warning is not applicable to the specific case (document why)
3. You have a better alternative (document the pattern)

Use `#pragma warning disable` with justification comments:

```csharp
// Justification: False positive - this property is initialized in Initialize()
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value
private string _connectionString;
#pragma warning restore CS8618
```

## Migration Guidelines

### Adding Breaking Changes

1. **Obsolete First**: Mark as `[Obsolete]` for at least one minor version
   ```csharp
   [Obsolete("Use NewMethod instead. This will be removed in v2.0.")]
   public void OldMethod() { }
   ```

2. **Provide Migration Path**: Document how to migrate in XML docs
3. **Increment Major Version**: When actually removing
4. **Update CHANGELOG**: Document all breaking changes

### Evolving Interfaces

Use default interface methods (C# 8.0+) to add members without breaking:

```csharp
public interface IMyInterface
{
    void ExistingMethod();

    // New method with default implementation (non-breaking)
    void NewMethod()
    {
        // Default implementation
    }
}
```

## Checklist for API Changes

Before committing API changes, verify:

- [ ] XML documentation is complete and accurate
- [ ] Nullable annotations are correct
- [ ] Access modifiers are appropriate (internal vs public)
- [ ] Breaking changes are documented in CHANGELOG
- [ ] Obsolete attributes added if deprecating
- [ ] Unit tests cover new API surface
- [ ] Sample code added for new public APIs
- [ ] All analyzer warnings resolved or suppressed with justification

## References

- [.NET API Design Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/)
- [Semantic Versioning](https://semver.org/)
- [Nullable Reference Types](https://docs.microsoft.com/en-us/dotnet/csharp/nullable-references)
- [.NET Breaking Change Rules](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/breaking-change-rules.md)
