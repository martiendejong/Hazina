using Hazina.CodeGeneration.Core.Models;

namespace Hazina.CodeGeneration.Core.Templates;

/// <summary>
/// Interface for code generation templates
/// </summary>
public interface ICodeTemplate
{
    /// <summary>
    /// The type of intent this template handles
    /// </summary>
    IntentType SupportedIntentType { get; }

    /// <summary>
    /// Generate code from an intent
    /// </summary>
    /// <param name="intent">The code generation intent</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated code</returns>
    Task<string> GenerateAsync(
        CodeGenerationIntent intent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate that this template can handle the given intent
    /// </summary>
    /// <param name="intent">The intent to validate</param>
    /// <returns>True if this template can handle the intent</returns>
    bool CanHandle(CodeGenerationIntent intent);
}
