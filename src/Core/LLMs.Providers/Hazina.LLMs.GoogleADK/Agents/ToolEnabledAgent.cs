using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.Tools.Registry;
using Hazina.LLMs.GoogleADK.Tools.Validation;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Agents;

/// <summary>
/// Enhanced LLM agent with integrated tool registry and validation
/// </summary>
public class ToolEnabledAgent : LlmAgent
{
    private readonly ToolRegistry _toolRegistry;
    private readonly ToolValidator _validator;
    private readonly SchemaManager _schemaManager;
    private readonly bool _autoDiscoverTools;

    public ToolEnabledAgent(
        string name,
        ILLMClient llmClient,
        ToolRegistry toolRegistry,
        AgentContext? context = null,
        bool autoDiscoverTools = true,
        int maxHistorySize = 50) : base(name, llmClient, context, maxHistorySize)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _validator = new ToolValidator(context?.Logger);
        _schemaManager = new SchemaManager(context?.Logger);
        _autoDiscoverTools = autoDiscoverTools;
    }

    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        // Auto-discover tools if enabled
        if (_autoDiscoverTools)
        {
            Context.Log(LogLevel.Information, "Auto-discovering tools from registry...");
            await _toolRegistry.DiscoverToolsAsync(cancellationToken);
        }

        // Load tools from registry
        var tools = _toolRegistry.GetAllTools();
        Context.Log(LogLevel.Information, "Loaded {Count} tools from registry", tools.Count);

        // Validate all tools
        var validTools = new List<HazinaChatTool>();
        foreach (var tool in tools)
        {
            var validationResult = _validator.ValidateToolDefinition(tool);

            if (validationResult.IsValid)
            {
                validTools.Add(tool);

                // Generate and register schema
                var schema = _schemaManager.GenerateSchema(tool.Parameters);
                _schemaManager.RegisterSchema(tool.FunctionName, schema);
            }
            else
            {
                Context.Log(LogLevel.Warning,
                    "Tool '{ToolName}' failed validation: {ValidationResult}",
                    tool.FunctionName,
                    validationResult);
            }
        }

        // Create tools context with validated tools
        if (validTools.Any())
        {
            var toolsContext = new ToolsContext();
            foreach (var tool in validTools)
            {
                toolsContext.Add(tool);
            }

            SetToolsContext(toolsContext);
            Context.Log(LogLevel.Information, "Enabled {Count} validated tools", validTools.Count);
        }

        await base.OnInitializeAsync(cancellationToken);
    }

    /// <summary>
    /// Add a tool to the agent's registry
    /// </summary>
    public void AddTool(HazinaChatTool tool, ToolMetadata? metadata = null)
    {
        var validationResult = _validator.ValidateToolDefinition(tool);

        if (!validationResult.IsValid)
        {
            throw new ArgumentException($"Tool validation failed: {validationResult}", nameof(tool));
        }

        _toolRegistry.RegisterTool(tool, metadata);

        // Generate schema
        var schema = _schemaManager.GenerateSchema(tool.Parameters);
        _schemaManager.RegisterSchema(tool.FunctionName, schema);

        Context.Log(LogLevel.Information, "Added tool: {ToolName}", tool.FunctionName);
    }

    /// <summary>
    /// Remove a tool from the agent
    /// </summary>
    public void RemoveTool(string toolName)
    {
        _toolRegistry.UnregisterTool(toolName);
        Context.Log(LogLevel.Information, "Removed tool: {ToolName}", toolName);
    }

    /// <summary>
    /// Search for tools by query
    /// </summary>
    public List<HazinaChatTool> SearchTools(string query)
    {
        return _toolRegistry.SearchTools(query);
    }

    /// <summary>
    /// Get tools by category
    /// </summary>
    public List<HazinaChatTool> GetToolsByCategory(string category)
    {
        return _toolRegistry.GetToolsByCategory(category);
    }

    /// <summary>
    /// Get tool registry statistics
    /// </summary>
    public RegistryStatistics GetToolStatistics()
    {
        return _toolRegistry.GetStatistics();
    }
}
