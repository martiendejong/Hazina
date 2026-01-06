using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hazina.AI.PromptManagement.Core;

/// <summary>
/// Interface for template rendering engines (Handlebars, Liquid, Scriban)
/// </summary>
public interface ITemplateEngine
{
    /// <summary>
    /// Name of the engine: "Handlebars", "Liquid", "Scriban"
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Render a template with given variables
    /// </summary>
    /// <param name="template">Template string</param>
    /// <param name="variables">Variables to substitute</param>
    /// <returns>Rendered string</returns>
    Task<string> RenderAsync(string template, Dictionary<string, object> variables);

    /// <summary>
    /// Validate template syntax
    /// </summary>
    /// <param name="template">Template string</param>
    /// <returns>True if valid, false otherwise</returns>
    Task<bool> ValidateAsync(string template);

    /// <summary>
    /// Extract variable names from template
    /// </summary>
    /// <param name="template">Template string</param>
    /// <returns>List of variable names used in the template</returns>
    Task<List<string>> ExtractVariablesAsync(string template);
}

/// <summary>
/// Factory for creating template engines
/// </summary>
public interface ITemplateEngineFactory
{
    /// <summary>
    /// Get a template engine by name
    /// </summary>
    /// <param name="engineName">Engine name: "Handlebars", "Liquid", "Scriban"</param>
    /// <returns>Template engine instance</returns>
    ITemplateEngine GetEngine(string engineName);

    /// <summary>
    /// Register a custom template engine
    /// </summary>
    /// <param name="engine">Template engine implementation</param>
    void RegisterEngine(ITemplateEngine engine);
}
