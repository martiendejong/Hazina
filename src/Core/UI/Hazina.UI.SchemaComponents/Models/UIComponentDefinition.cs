using System.Text.Json;

namespace Hazina.UI.SchemaComponents.Models;

/// <summary>
/// Schema-driven UI component definition.
/// Defines a reusable UI component with JSON Schema validation, security rules, and behavioral constraints.
/// </summary>
public record UIComponentDefinition
{
    /// <summary>
    /// Unique identifier for this component (e.g., "ui.task-card", "ui.file-tree").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Component type classification (e.g., "visualization", "input", "control").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Functional category (e.g., "task", "file", "approval", "monitoring").
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Human-readable description of component's purpose.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// JSON Schema defining the component's data contract.
    /// Validates component state at runtime.
    /// </summary>
    public required JsonDocument Schema { get; init; }

    /// <summary>
    /// Optional validation rules for component behavior.
    /// </summary>
    public ValidationRules? Validation { get; init; }

    /// <summary>
    /// Security and approval rules for component usage.
    /// </summary>
    public SecurityRules Security { get; init; } = new()
    {
        RequiresApproval = false,
        AllowedOperations = new List<string> { "view" },
        RiskLevel = "read"
    };
}

/// <summary>
/// Container for validation rules that enforce component constraints.
/// </summary>
public record ValidationRules
{
    public List<ValidationRule> Rules { get; init; } = new();
}

/// <summary>
/// Individual validation rule with condition and message.
/// </summary>
public record ValidationRule
{
    /// <summary>
    /// Name identifying this validation rule.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Condition expression (e.g., "riskLevel == 'admin'").
    /// </summary>
    public required string Condition { get; init; }

    /// <summary>
    /// Required state when condition is true (e.g., "requiresApproval == true").
    /// </summary>
    public required string Requires { get; init; }

    /// <summary>
    /// Error message displayed when validation fails.
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// Security rules governing component usage and user approval requirements.
/// </summary>
public record SecurityRules
{
    /// <summary>
    /// Whether user approval is required before rendering this component.
    /// </summary>
    public bool RequiresApproval { get; init; }

    /// <summary>
    /// List of operations allowed on this component (e.g., "view", "approve", "reject", "execute").
    /// </summary>
    public List<string> AllowedOperations { get; init; } = new();

    /// <summary>
    /// Risk level classification: "read", "write", "execute", "admin".
    /// </summary>
    public required string RiskLevel { get; init; }
}
