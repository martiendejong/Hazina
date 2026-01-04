using Hazina.CodeGeneration.Core.Models;

namespace Hazina.CodeGeneration.Core.Templates;

/// <summary>
/// Interface for the template engine that generates code from intents
/// </summary>
public interface ITemplateEngine
{
    /// <summary>
    /// Register a code template
    /// </summary>
    /// <param name="template">The template to register</param>
    void RegisterTemplate(ICodeTemplate template);

    /// <summary>
    /// Generate code from an intent
    /// </summary>
    /// <param name="intent">The code generation intent</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated code</returns>
    Task<string> GenerateCodeAsync(
        CodeGenerationIntent intent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Format generated code
    /// </summary>
    /// <param name="code">The code to format</param>
    /// <returns>Formatted code</returns>
    string FormatCode(string code);
}
