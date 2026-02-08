using Hazina.CodeGeneration.Core.Models;
using Hazina.UI.SchemaComponents.Models;
using Hazina.UI.SchemaComponents.Registry;

namespace Hazina.UI.SchemaComponents.Translation;

/// <summary>
/// Default implementation of intent-to-UI translation.
/// Maps code generation intents to appropriate UI component combinations.
/// </summary>
public class IntentToUITranslator : IIntentToUITranslator
{
    private readonly IUIComponentRegistry _registry;

    public IntentToUITranslator(IUIComponentRegistry registry)
    {
        _registry = registry;
    }

    public async Task<UIComponentDefinition[]> TranslateIntentAsync(CodeGenerationIntent intent)
    {
        // Determine which UI components are appropriate for this intent
        var components = new List<UIComponentDefinition>();

        // All intents get a task card showing breakdown
        components.Add(_registry.GetComponent("ui.task-card"));

        // Route based on intent type
        switch (intent.Type)
        {
            case IntentType.GenerateClass:
            case IntentType.GenerateInterface:
            case IntentType.GenerateModel:
                // File generation intents need file tree and approval
                components.Add(_registry.GetComponent("ui.file-tree"));
                components.Add(_registry.GetComponent("ui.approval-panel"));
                break;

            case IntentType.GenerateMethod:
                // Method generation is typically lower risk (modifying existing file)
                components.Add(_registry.GetComponent("ui.file-tree"));
                if (RequiresApproval(intent))
                {
                    components.Add(_registry.GetComponent("ui.approval-panel"));
                }
                break;

            case IntentType.GenerateDocumentation:
                // Documentation is read-only analysis, just show file tree
                components.Add(_registry.GetComponent("ui.file-tree"));
                break;

            case IntentType.RefactorCode:
                // Refactoring is high risk, needs approval and progress tracking
                components.Add(_registry.GetComponent("ui.file-tree"));
                components.Add(_registry.GetComponent("ui.approval-panel"));
                components.Add(_registry.GetComponent("ui.progress-tracker"));
                break;

            case IntentType.GenerateTests:
                // Test generation needs approval and progress (can create many files)
                components.Add(_registry.GetComponent("ui.file-tree"));
                components.Add(_registry.GetComponent("ui.approval-panel"));
                components.Add(_registry.GetComponent("ui.progress-tracker"));
                break;

            default:
                // Default: just task card
                break;
        }

        // If intent involves command execution, add command preview
        if (RequiresExecution(intent))
        {
            components.Add(_registry.GetComponent("ui.command-preview"));
        }

        return await Task.FromResult(components.ToArray());
    }

    public string DetermineRiskLevel(CodeGenerationIntent intent)
    {
        // Determine risk based on intent type and context
        return intent.Type switch
        {
            IntentType.GenerateDocumentation => "read",
            IntentType.GenerateClass or IntentType.GenerateInterface or IntentType.GenerateModel => "write",
            IntentType.GenerateMethod => "write",
            IntentType.GenerateTests => "write",
            IntentType.RefactorCode => "execute", // Higher risk due to modifying existing code
            _ => "write"
        };
    }

    public bool RequiresApproval(CodeGenerationIntent intent)
    {
        // Determine if approval needed based on risk level
        var riskLevel = DetermineRiskLevel(intent);

        return riskLevel switch
        {
            "read" => false,
            "write" => true,
            "execute" => true,
            "admin" => true,
            _ => true
        };
    }

    /// <summary>
    /// Determine if intent involves system command execution.
    /// </summary>
    private bool RequiresExecution(CodeGenerationIntent intent)
    {
        // Check if context contains command/script parameters
        return intent.Context.ContainsKey("command") ||
               intent.Context.ContainsKey("script") ||
               intent.Context.ContainsKey("executable");
    }
}
