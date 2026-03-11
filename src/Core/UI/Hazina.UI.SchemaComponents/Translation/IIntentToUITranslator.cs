using Hazina.CodeGeneration.Core.Models;
using Hazina.UI.SchemaComponents.Models;

namespace Hazina.UI.SchemaComponents.Translation;

/// <summary>
/// Translates code generation intents into appropriate UI component definitions.
/// Maps user requests to schema-driven UI components for transparent agent operation.
/// </summary>
public interface IIntentToUITranslator
{
    /// <summary>
    /// Translate a code generation intent into the UI components needed to visualize it.
    /// </summary>
    /// <param name="intent">Parsed code generation intent.</param>
    /// <returns>Array of UI component definitions to render for this intent.</returns>
    Task<UIComponentDefinition[]> TranslateIntentAsync(CodeGenerationIntent intent);

    /// <summary>
    /// Determine the risk level of an intent for approval workflow routing.
    /// </summary>
    /// <param name="intent">Code generation intent to evaluate.</param>
    /// <returns>Risk level: read, write, execute, or admin.</returns>
    string DetermineRiskLevel(CodeGenerationIntent intent);

    /// <summary>
    /// Check if an intent requires user approval before execution.
    /// </summary>
    /// <param name="intent">Intent to evaluate.</param>
    /// <returns>True if approval required; false otherwise.</returns>
    bool RequiresApproval(CodeGenerationIntent intent);
}
