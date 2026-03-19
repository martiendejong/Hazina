# Development Guide

## Overview

This guide covers development workflows, code quality standards, and tooling for contributing to the Hazina AI Framework.

## Prerequisites

- .NET 8.0, 9.0, or 10.0 SDK
- Visual Studio 2022 17.8+, VS Code, or Rider 2023.3+
- Git

## Getting Started

```bash
# Clone the repository
git clone https://github.com/martiendejong/Hazina.git
cd Hazina

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test
```

## Project Structure

```
Hazina/
├── src/
│   ├── Core/                  # Core framework assemblies
│   │   ├── AI/               # AI-related packages
│   │   ├── Agents/           # Agent lifecycle and factory
│   │   ├── LLMs/             # LLM client and providers
│   │   ├── Storage/          # Storage abstractions
│   │   └── Observability/    # Logging and monitoring
│   ├── Apps/                 # Application packages
│   └── Tools/                # Utility packages
├── Tests/                    # Test projects
├── docs/                     # Documentation
└── Directory.Build.props     # Shared build configuration
```

## Code Quality Standards

### Nullable Reference Types

All projects have nullable reference types enabled by default via `Directory.Build.props`:

```xml
<Nullable>enable</Nullable>
```

**Guidelines**:
- Use `?` suffix for nullable reference types: `string? nullableString`
- Use `!` null-forgiving operator sparingly and only when you're certain
- Prefer null-conditional operators: `obj?.Property`
- Use null-coalescing assignment: `value ??= defaultValue`

**Nullable Warnings as Errors**:
The following nullable warnings are treated as errors:
- CS8600: Converting null literal or possible null value to non-nullable type
- CS8601: Possible null reference assignment
- CS8602: Dereference of a possibly null reference
- CS8603: Possible null reference return
- CS8604: Possible null reference argument

### Roslyn Analyzers

Hazina uses multiple Roslyn analyzers to enforce code quality standards:

#### 1. **SonarAnalyzer.CSharp** (Security, Bugs, Code Smells)
- Detects security vulnerabilities (SQL injection, weak crypto)
- Identifies potential bugs (NullReferenceException, deadlocks)
- Flags code smells (complexity, duplication, naming)

Key Rules:
- S3776: Cognitive complexity should not exceed 15
- S138: Methods should not have more than 30 lines
- S107: Methods should not have more than 4 parameters
- S134: Nested blocks depth should not exceed 3

#### 2. **StyleCop.Analyzers** (Code Style)
- Enforces consistent code style and documentation
- Validates XML documentation comments
- Checks naming conventions and layout

Key Rules:
- SA1600-SA1651: XML documentation rules (suggestion)
- SA1500-SA1503: Brace layout rules (warning)
- SA1200: Using directives placement (disabled)
- SA1309: Field naming (disabled - we use `_camelCase`)

#### 3. **Meziantou.Analyzer** (Best Practices)
- Enforces .NET best practices
- Performance optimization suggestions
- Modern C# feature usage

#### 4. **AsyncFixer** (Async/Await Best Practices)
- Detects fire-and-forget async operations
- Identifies blocking calls in async methods
- Prevents async void methods (except event handlers)

**Critical Rules**:
- AsyncFixer03: **Fire-and-forget async void methods** (ERROR)
  ```csharp
  // ❌ BAD - will crash app if exception thrown
  async void ProcessData() { }

  // ✅ GOOD - returns Task for proper error handling
  async Task ProcessDataAsync() { }
  ```

- AsyncFixer01: Unnecessary async/await (WARNING)
  ```csharp
  // ❌ BAD - unnecessary async/await
  async Task<string> GetDataAsync()
  {
      return await service.GetAsync();
  }

  // ✅ GOOD - direct task return
  Task<string> GetDataAsync()
  {
      return service.GetAsync();
  }
  ```

- AsyncFixer02: Long-running operation inside async method (WARNING)
  ```csharp
  // ❌ BAD - blocks thread pool
  async Task ProcessAsync()
  {
      Thread.Sleep(1000);
  }

  // ✅ GOOD - truly async
  async Task ProcessAsync()
  {
      await Task.Delay(1000);
  }
  ```

#### 5. **Roslynator.Analyzers** (Code Quality & Refactoring)
- Simplification suggestions
- Null safety improvements
- Modern C# feature adoption

**Key Rules**:
- RCS1202: Avoid NullReferenceException (WARNING)
- RCS1210: Return Task.FromResult instead of null (ERROR)
- RCS1194: Implement exception constructors (WARNING)
- RCS1146: Use conditional access (SUGGESTION)

### Analyzer Severity Levels

Analyzers are configured in `.editorconfig` with the following severity levels:

- **Error**: Build will fail. Must be fixed before merging.
  - Async void methods (AsyncFixer03)
  - Returning null instead of Task.FromResult (RCS1210)
  - Nullable reference violations (CS8600-CS8604)

- **Warning**: Should be fixed before merging. May be suppressed with justification.
  - Dispose objects before losing scope (CA2000)
  - Fire-and-forget in using block (AsyncFixer04)
  - SQL injection vulnerabilities (CA2100)

- **Suggestion**: Consider fixing. Won't block merge.
  - Complexity metrics (CA1502, S3776)
  - Code style preferences (SA1201, RCS1146)
  - Documentation completeness (SA1600)

### Suppressing Warnings

When you must suppress a warning, use `#pragma warning disable` with justification:

```csharp
// Justification: Legacy code compatibility requirement
#pragma warning disable CA1062 // Validate arguments of public methods
public void ProcessLegacyData(object data)
{
    // Legacy implementation
}
#pragma warning restore CA1062
```

Alternatively, use `[SuppressMessage]` attribute:

```csharp
[SuppressMessage("Performance", "CA1822:Mark members as static",
    Justification = "Instance method required for interface implementation")]
public string GetName() => _name;
```

## Build Configuration

### Directory.Build.props

All projects inherit common settings from `Directory.Build.props`:

```xml
<PropertyGroup>
  <!-- Language Features -->
  <LangVersion>latest</LangVersion>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>

  <!-- Code Analysis -->
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  <EnableNETAnalyzers>true</EnableNETAnalyzers>
  <AnalysisLevel>latest-all</AnalysisLevel>

  <!-- Documentation -->
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

### Multi-Targeting

Core assemblies target multiple .NET versions for broad compatibility:

```xml
<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
```

Use conditional compilation when needed:

```csharp
#if NET8_0_OR_GREATER
    // .NET 8+ specific code
#endif
```

## Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true

# Run specific test project
dotnet test Tests/Hazina.LLMs.Tests
```

### Test Conventions

- Use xUnit for all tests
- Follow AAA pattern: Arrange, Act, Assert
- Use meaningful test names: `MethodName_Scenario_ExpectedBehavior`
- Mock external dependencies (LLM APIs, databases)

Example:

```csharp
[Fact]
public async Task GenerateAsync_WithValidPrompt_ReturnsResponse()
{
    // Arrange
    var client = new MockLlmClient();
    var prompt = "Test prompt";

    // Act
    var response = await client.GenerateAsync(prompt);

    // Assert
    Assert.NotNull(response);
    Assert.NotEmpty(response.Content);
}
```

## Code Review Checklist

Before submitting a PR, ensure:

- [ ] Code builds without errors
- [ ] All tests pass
- [ ] No new analyzer warnings (or justified suppressions)
- [ ] XML documentation added for public APIs
- [ ] Nullable annotations correct
- [ ] No async void methods (except event handlers)
- [ ] Long-running operations use async/await (no Thread.Sleep)
- [ ] Proper disposal of IDisposable resources
- [ ] CancellationToken support for async operations

## API Design Guidelines

### Experimental APIs

Mark experimental APIs with `[Experimental("HAZXXX")]`:

```csharp
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Experimental cognitive pipeline.
/// </summary>
/// <remarks>
/// This is an experimental API and may change in future versions without notice.
/// </remarks>
[Experimental("HAZ001")]
public interface ICognitivePipeline
{
    Task<CognitivePipelineResult> ExecuteAsync(SCPContext context);
}
```

See [API_STABILITY.md](API_STABILITY.md) for experimental API codes.

### Async Methods

- Always suffix async methods with `Async`
- Return `Task` or `Task<T>`, never `async void`
- Accept `CancellationToken` as last parameter (default: `default`)
- Forward CancellationToken to all async operations

```csharp
public async Task<LlmResponse> GenerateAsync(
    LlmRequest request,
    CancellationToken cancellationToken = default)
{
    await ValidateAsync(request, cancellationToken);
    return await client.SendAsync(request, cancellationToken);
}
```

### Nullable Parameters

- Use nullable reference types for optional parameters
- Prefer nullable over sentinel values (empty string, -1, etc.)
- Document null behavior in XML comments

```csharp
/// <summary>
/// Gets context for a query.
/// </summary>
/// <param name="query">Query text (required)</param>
/// <param name="embedding">Optional pre-computed embedding</param>
/// <returns>Context string</returns>
public Task<string> GetContextAsync(string query, float[]? embedding = null)
```

## Performance Best Practices

### Async/Await

- Avoid `async void` - use `async Task` instead
- Don't block on async code (`Wait()`, `Result`)
- Use `ConfigureAwait(false)` in libraries (disabled via analyzer in ASP.NET Core apps)
- Avoid unnecessary async/await (return Task directly when possible)

### Memory Management

- Dispose `IDisposable` resources properly
- Use `using` statements or `using` declarations
- Consider `ArrayPool<T>` for temporary buffers
- Avoid string concatenation in loops (use `StringBuilder`)

### Collections

- Avoid multiple enumeration of `IEnumerable<T>` - materialize to array/list if needed
- Use `Span<T>` and `Memory<T>` for high-performance scenarios
- Prefer `IReadOnlyList<T>` for read-only collections

## CI/CD Integration

### GitHub Actions

Pull requests trigger:
1. Build validation (all target frameworks)
2. Test execution (unit + integration)
3. Analyzer validation (errors block merge)
4. API compatibility check (via ApiCompat)

### Local Pre-Commit Checks

Recommended Git pre-commit hook:

```bash
#!/bin/sh
dotnet build --no-restore
if [ $? -ne 0 ]; then
    echo "Build failed. Commit aborted."
    exit 1
fi

dotnet test --no-build --no-restore
if [ $? -ne 0 ]; then
    echo "Tests failed. Commit aborted."
    exit 1
fi
```

## IDE Setup

### Visual Studio

1. Install latest Visual Studio 2022 (17.8+)
2. Enable EditorConfig support: Tools → Options → Text Editor → C# → Code Style → General → "Follow project coding conventions"
3. Enable analyzers: Tools → Options → Text Editor → C# → Advanced → "Run code analysis in background"

### VS Code

1. Install C# extension (ms-dotnettools.csharp)
2. Install EditorConfig extension (EditorConfig.EditorConfig)
3. Enable format on save in settings.json:
   ```json
   {
     "editor.formatOnSave": true,
     "omnisharp.enableEditorConfigSupport": true
   }
   ```

### Rider

1. Use Rider 2023.3+
2. EditorConfig is automatically enabled
3. Configure inspections: File → Settings → Editor → Inspection Settings → Use .editorconfig

## Troubleshooting

### Build Issues

**Problem**: Nullable reference warnings after pulling latest code

**Solution**: Clean and rebuild
```bash
dotnet clean
dotnet restore
dotnet build
```

**Problem**: Analyzer warnings in generated code

**Solution**: Add `[ExcludeFromCodeCoverage]` or `[GeneratedCode]` attribute:
```csharp
[ExcludeFromCodeCoverage]
public partial class GeneratedClass { }
```

**Problem**: Too many analyzer warnings

**Solution**: Fix errors first, then address warnings incrementally. Configure `.editorconfig` to set overwhelming rules to `suggestion` temporarily.

### Analyzer Issues

**Problem**: False positive analyzer warnings

**Solution**: Suppress with justification:
```csharp
#pragma warning disable RCS1090 // Call ConfigureAwait(false)
// Justification: ASP.NET Core app - ConfigureAwait not needed
await ProcessAsync();
#pragma warning restore RCS1090
```

**Problem**: Analyzer slowing down IDE

**Solution**: Disable specific analyzers in `.editorconfig`:
```ini
dotnet_diagnostic.SA1600.severity = none  # Disable XML doc requirement
```

## Resources

- [API Stability Policy](API_STABILITY.md)
- [Architecture Documentation](ARCHITECTURE.md)
- [Getting Started Guide](GETTING_STARTED.md)
- [Testing Guide](TESTING.md)

## Questions?

- Open a [GitHub Discussion](https://github.com/martiendejong/Hazina/discussions)
- File an [Issue](https://github.com/martiendejong/Hazina/issues)
- Review [Contributing Guidelines](../CONTRIBUTING.md)

---

**Last Updated**: 2026-03-19
