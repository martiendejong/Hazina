using System.Text.Json;
using Hazina.UI.SchemaComponents.Models;

namespace Hazina.UI.SchemaComponents.Registry;

/// <summary>
/// Registry for managing UI component definitions.
/// Provides lookup, validation, and custom component registration.
/// </summary>
public interface IUIComponentRegistry
{
    /// <summary>
    /// Retrieve a component definition by its unique ID.
    /// </summary>
    /// <param name="componentId">Component identifier (e.g., "ui.task-card").</param>
    /// <returns>Component definition.</returns>
    /// <exception cref="KeyNotFoundException">When component ID not found.</exception>
    UIComponentDefinition GetComponent(string componentId);

    /// <summary>
    /// Get all components in a specific category.
    /// </summary>
    /// <param name="category">Category filter (e.g., "task", "file", "approval").</param>
    /// <returns>Enumerable of matching components.</returns>
    IEnumerable<UIComponentDefinition> GetByCategory(string category);

    /// <summary>
    /// Validate component state against its JSON Schema.
    /// </summary>
    /// <param name="componentId">Component identifier.</param>
    /// <param name="state">State data to validate.</param>
    /// <returns>True if state is valid; false otherwise.</returns>
    bool ValidateComponentState(string componentId, JsonElement state);

    /// <summary>
    /// Register a custom component definition at runtime.
    /// </summary>
    /// <param name="definition">Component definition to register.</param>
    /// <returns>The registered component definition.</returns>
    /// <exception cref="InvalidOperationException">When component ID already exists.</exception>
    UIComponentDefinition RegisterCustomComponent(UIComponentDefinition definition);

    /// <summary>
    /// Get all registered component IDs.
    /// </summary>
    /// <returns>Enumerable of component IDs.</returns>
    IEnumerable<string> GetAllComponentIds();
}
