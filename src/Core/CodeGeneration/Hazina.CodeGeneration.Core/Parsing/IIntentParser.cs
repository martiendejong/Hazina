using Hazina.CodeGeneration.Core.Models;

namespace Hazina.CodeGeneration.Core.Parsing;

/// <summary>
/// Interface for parsing natural language into code generation intents
/// </summary>
public interface IIntentParser
{
    /// <summary>
    /// Parse a natural language prompt into a code generation intent
    /// </summary>
    /// <param name="prompt">The natural language prompt</param>
    /// <param name="context">Additional context for parsing</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The parsed intent</returns>
    Task<CodeGenerationIntent> ParseAsync(
        string prompt,
        Dictionary<string, string>? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect the type of intent from the prompt
    /// </summary>
    /// <param name="prompt">The natural language prompt</param>
    /// <returns>The detected intent type</returns>
    IntentType DetectIntentType(string prompt);

    /// <summary>
    /// Validate that an intent has all required information
    /// </summary>
    /// <param name="intent">The intent to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    bool ValidateIntent(CodeGenerationIntent intent);
}
