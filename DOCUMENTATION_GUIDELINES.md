# Documentation Guidelines for Hazina Framework

This document outlines the requirements and best practices for documenting code in the Hazina framework.

## Why Documentation Matters

Good documentation:
- Helps users understand how to use the framework
- Makes the codebase easier to maintain
- Improves discoverability through IntelliSense
- Enables automatic API reference generation

## XML Documentation Comments

All **public** classes, interfaces, methods, and properties must include XML documentation comments.

### Basic Structure

```csharp
/// <summary>
/// Brief description of what this class/method does.
/// </summary>
/// <remarks>
/// Optional: More detailed explanation, usage notes, or examples.
/// </remarks>
public class MyClass
{
    /// <summary>
    /// Gets or sets the name property.
    /// </summary>
    /// <value>
    /// The name as a string.
    /// </value>
    public string Name { get; set; }

    /// <summary>
    /// Processes the input data and returns a result.
    /// </summary>
    /// <param name="input">The input string to process.</param>
    /// <param name="options">Optional processing options.</param>
    /// <returns>The processed result as a string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is null.</exception>
    /// <example>
    /// <code>
    /// var myClass = new MyClass();
    /// var result = myClass.Process("test", new ProcessOptions());
    /// </code>
    /// </example>
    public string Process(string input, ProcessOptions? options = null)
    {
        // Implementation
    }
}
```

### Required Tags

| Tag | When to Use |
|-----|-------------|
| `<summary>` | **REQUIRED** for all public members. Brief, one-sentence description. |
| `<param>` | **REQUIRED** for each method parameter. |
| `<returns>` | **REQUIRED** for methods that return a value (not void). |
| `<exception>` | Document exceptions that callers should handle. |
| `<remarks>` | Additional details, usage notes, or important information. |
| `<example>` | Usage examples (highly recommended for complex APIs). |
| `<see cref="">` | Link to related types or members. |
| `<value>` | Describe what a property represents. |

### Guidelines

1. **Be Concise but Clear**
   - Summary should be one sentence
   - Use remarks for detailed explanations

2. **Use Active Voice**
   - Good: "Gets the user's name"
   - Bad: "The user's name is gotten"

3. **Document Intent, Not Implementation**
   - Focus on WHAT it does and WHY
   - Not HOW it does it (that's what the code shows)

4. **Provide Examples for Complex APIs**
   ```csharp
   /// <example>
   /// <code>
   /// var agent = new Agent("MyAgent");
   /// await agent.RunAsync("Process this text");
   /// </code>
   /// </example>
   ```

5. **Link to Related Types**
   ```csharp
   /// <see cref="ILLMClient"/> for the LLM client interface.
   /// <seealso cref="AgentWorkspace"/>
   ```

### Common Patterns

#### Interfaces
```csharp
/// <summary>
/// Defines the contract for LLM client implementations.
/// </summary>
/// <remarks>
/// Implement this interface to add support for a new LLM provider.
/// </remarks>
public interface ILLMClient
{
    /// <summary>
    /// Sends a completion request to the LLM.
    /// </summary>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The LLM response.</returns>
    Task<CompletionResponse> CompleteAsync(string prompt, CancellationToken cancellationToken = default);
}
```

#### Abstract Classes
```csharp
/// <summary>
/// Base class for all LLM provider implementations.
/// </summary>
/// <remarks>
/// Inherit from this class to create a new LLM provider.
/// Override <see cref="CompleteInternalAsync"/> to implement provider-specific logic.
/// </remarks>
public abstract class LLMProviderBase : ILLMClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LLMProviderBase"/> class.
    /// </summary>
    /// <param name="config">The provider configuration.</param>
    protected LLMProviderBase(IProviderConfig config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }
}
```

#### Extension Methods
```csharp
/// <summary>
/// Provides extension methods for <see cref="ILLMClient"/>.
/// </summary>
public static class LLMClientExtensions
{
    /// <summary>
    /// Sends a completion request with streaming enabled.
    /// </summary>
    /// <param name="client">The LLM client.</param>
    /// <param name="prompt">The input prompt.</param>
    /// <returns>An async enumerable of completion chunks.</returns>
    public static IAsyncEnumerable<string> StreamAsync(this ILLMClient client, string prompt)
    {
        // Implementation
    }
}
```

## Markdown Documentation

In addition to XML comments, maintain conceptual documentation in the `/docs` folder:

- **Guides** - How-to guides for common tasks
- **Architecture** - System design and architecture decisions
- **Tutorials** - Step-by-step learning materials
- **API Reference** - Auto-generated from XML comments

## Documentation Generation

Documentation is generated using DocFX. To generate the documentation:

```bash
# Enable XML documentation generation (one-time setup)
.\enable-xml-docs.ps1

# Generate documentation
.\generate-docs.ps1

# Generate and preview
.\generate-docs.ps1 -Serve
```

## Pull Request Requirements

**Before creating a PR, ensure:**

1. ✅ All public APIs have XML documentation
2. ✅ Complex features have usage examples
3. ✅ New features are documented in `/docs` guides
4. ✅ Documentation builds without errors: `.\generate-docs.ps1`
5. ✅ No CS1591 warnings (missing XML comments)

## Exceptions

The following do NOT require XML documentation:
- Private members
- Internal types (unless used by other libraries)
- Test code
- Generated code (marked with `[GeneratedCode]` attribute)

## Resources

- [C# XML Documentation Comments](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/)
- [DocFX Documentation](https://dotnet.github.io/docfx/)
- [Hazina Documentation](https://your-docs-site.com)

## Enforcement

- **Pre-commit hooks** - Check for missing documentation
- **CI/CD pipeline** - Documentation must build successfully
- **Code reviews** - Reviewers check documentation quality

---

**Remember:** Documentation is code. Treat it with the same care as your implementation.
