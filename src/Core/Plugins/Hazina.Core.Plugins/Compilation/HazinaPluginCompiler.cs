using System.Reflection;
using System.Runtime.Loader;
using Hazina.Core.Plugins.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.Logging;

namespace Hazina.Core.Plugins.Compilation;

/// <summary>
/// Compiles C# code into executable plugins using Roslyn
/// </summary>
public class HazinaPluginCompiler
{
    private readonly ILogger<HazinaPluginCompiler> _logger;
    private readonly List<MetadataReference> _references;

    public HazinaPluginCompiler(ILogger<HazinaPluginCompiler> logger)
    {
        _logger = logger;
        _references = BuildDefaultReferences();
    }

    /// <summary>
    /// Compile C# source code into a plugin
    /// </summary>
    public async Task<CompiledPlugin> CompileAsync(PluginMetadata metadata)
    {
        try
        {
            _logger.LogInformation("Compiling plugin: {PluginName}", metadata.Name);

            // Wrap user code in plugin class structure
            var wrappedCode = WrapInPluginClass(metadata);

            // Parse the code
            var syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode);

            // Create compilation
            var compilation = CSharpCompilation.Create(
                assemblyName: $"HazinaPlugin_{metadata.Name}_{Guid.NewGuid():N}",
                syntaxTrees: new[] { syntaxTree },
                references: _references,
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    checkOverflow: true,
                    allowUnsafe: false
                )
            );

            // Check for compilation errors
            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (diagnostics.Any())
            {
                var errors = string.Join("\n", diagnostics.Select(d => d.ToString()));
                _logger.LogError("Plugin compilation failed:\n{Errors}", errors);
                throw new PluginCompilationException($"Compilation failed:\n{errors}");
            }

            // Emit to memory
            using var ms = new MemoryStream();
            EmitResult emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var failures = string.Join("\n", emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));
                _logger.LogError("Plugin emit failed:\n{Failures}", failures);
                throw new PluginCompilationException($"Emit failed:\n{failures}");
            }

            // Load assembly
            ms.Seek(0, SeekOrigin.Begin);
            var assemblyLoadContext = new AssemblyLoadContext($"Plugin_{metadata.Name}", isCollectible: true);
            var assembly = assemblyLoadContext.LoadFromStream(ms);

            _logger.LogInformation("Plugin compiled successfully: {PluginName}", metadata.Name);

            return new CompiledPlugin
            {
                Assembly = assembly,
                LoadContext = assemblyLoadContext,
                Metadata = metadata,
                CompiledAt = DateTime.UtcNow
            };
        }
        catch (PluginCompilationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error compiling plugin: {PluginName}", metadata.Name);
            throw new PluginCompilationException($"Unexpected compilation error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Wrap user code in a proper plugin class structure
    /// </summary>
    private string WrapInPluginClass(PluginMetadata metadata)
    {
        // Sanitize class name (remove invalid characters)
        var className = SanitizeClassName(metadata.Name);

        return $$"""
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Hazina.Core.Plugins.Abstractions;

namespace Hazina.DynamicPlugins
{
    public class {{className}}Plugin : IHazinaPlugin
    {
        public string Name => "{{metadata.Name}}";
        public string Version => "{{metadata.Version}}";
        public string Description => "{{metadata.Description?.Replace("\"", "\\\"") ?? "AI-generated plugin"}}";

        public async Task<PluginResult> ExecuteAsync(PluginContext context)
        {
            try
            {
                // ============================================
                // User-provided code starts here
                // ============================================

{{IndentCode(metadata.SourceCode, 4)}}

                // ============================================
                // User-provided code ends here
                // ============================================

                // If no explicit return, assume success
                return PluginResult.Successful();
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, "Plugin execution failed: {PluginName}", Name);
                return PluginResult.Failed($"Plugin execution failed: {ex.Message}", ex);
            }
        }
    }
}
""";
    }

    /// <summary>
    /// Sanitize class name to be valid C# identifier
    /// </summary>
    private string SanitizeClassName(string name)
    {
        // Remove invalid characters and ensure it starts with a letter
        var sanitized = new string(name
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray());

        if (!char.IsLetter(sanitized[0]))
        {
            sanitized = "Plugin_" + sanitized;
        }

        return sanitized;
    }

    /// <summary>
    /// Indent code by specified number of spaces
    /// </summary>
    private string IndentCode(string code, int spaces)
    {
        var indent = new string(' ', spaces);
        var lines = code.Split('\n');
        return string.Join('\n', lines.Select(line => indent + line));
    }

    /// <summary>
    /// Build default metadata references for compilation
    /// </summary>
    private List<MetadataReference> BuildDefaultReferences()
    {
        var references = new List<MetadataReference>
        {
            // Core .NET assemblies
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),

            // Plugin abstractions
            MetadataReference.CreateFromFile(typeof(IHazinaPlugin).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location),

            // System.Runtime
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
        };

        // Add netstandard reference if available
        try
        {
            references.Add(MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location));
        }
        catch
        {
            // netstandard not available, skip
        }

        return references;
    }

    /// <summary>
    /// Add additional assembly references for plugins
    /// </summary>
    public void AddReferences(params Type[] types)
    {
        foreach (var type in types)
        {
            var reference = MetadataReference.CreateFromFile(type.Assembly.Location);
            if (!_references.Any(r => r.Display == reference.Display))
            {
                _references.Add(reference);
                _logger.LogDebug("Added reference: {Assembly}", type.Assembly.FullName);
            }
        }
    }

    /// <summary>
    /// Add assembly reference by assembly location
    /// </summary>
    public void AddReference(string assemblyPath)
    {
        if (File.Exists(assemblyPath))
        {
            var reference = MetadataReference.CreateFromFile(assemblyPath);
            if (!_references.Any(r => r.Display == reference.Display))
            {
                _references.Add(reference);
                _logger.LogDebug("Added reference: {Path}", assemblyPath);
            }
        }
        else
        {
            _logger.LogWarning("Assembly not found: {Path}", assemblyPath);
        }
    }
}
