using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace DevGPT.LLMs.Plugins;

/// <summary>
/// Adapter that converts DevGPT IToolsContext tools to Semantic Kernel plugins dynamically
/// </summary>
public class ToolsContextPluginAdapter
{
    private readonly IToolsContext _toolsContext;

    public ToolsContextPluginAdapter(IToolsContext toolsContext)
    {
        _toolsContext = toolsContext;
    }

    /// <summary>
    /// Register all tools from IToolsContext as Semantic Kernel functions in the kernel
    /// </summary>
    public void RegisterToolsAsPlugins(Kernel kernel, List<DevGPTChatMessage> messages, CancellationToken cancellationToken)
    {
        if (_toolsContext?.Tools == null || !_toolsContext.Tools.Any())
            return;

        foreach (var tool in _toolsContext.Tools)
        {
            // Create a KernelFunction from the DevGPTChatTool
            var kernelFunction = CreateKernelFunction(tool, messages, cancellationToken);

            // Import the function into the kernel
            kernel.ImportPluginFromFunctions(tool.FunctionName, new[] { kernelFunction });
        }
    }

    /// <summary>
    /// Create a Semantic Kernel function from a DevGPTChatTool
    /// </summary>
    private KernelFunction CreateKernelFunction(
        DevGPTChatTool tool,
        List<DevGPTChatMessage> messages,
        CancellationToken cancellationToken)
    {
        // Build parameter metadata for SK
        var parameters = tool.Parameters.Select(p => new KernelParameterMetadata(p.Name)
        {
            Description = p.Description,
            IsRequired = p.Required,
            ParameterType = GetParameterType(p.Type)
        }).ToList();

        // Create the execution delegate that wraps the original tool
        var method = async (KernelFunction function, Kernel kernel, KernelArguments arguments, CancellationToken cancel) =>
        {
            // Convert KernelArguments to DevGPTChatToolCall format
            var toolCall = CreateToolCall(tool, arguments);

            // Execute the original tool
            var result = await tool.Execute(messages, toolCall, cancel);

            // Return as FunctionResult
            return new FunctionResult(function, result);
        };

        // Create the KernelFunction with metadata
        return KernelFunctionFactory.CreateFromMethod(
            method: method,
            functionName: tool.FunctionName,
            description: tool.Description,
            parameters: parameters,
            returnParameter: new KernelReturnParameterMetadata { ParameterType = typeof(string) }
        );
    }

    /// <summary>
    /// Create a DevGPTChatToolCall from Semantic Kernel arguments
    /// </summary>
    private DevGPTChatToolCall CreateToolCall(DevGPTChatTool tool, KernelArguments arguments)
    {
        // Build JSON arguments from KernelArguments
        var argumentsDict = new Dictionary<string, object?>();

        foreach (var param in tool.Parameters)
        {
            if (arguments.TryGetValue(param.Name, out var value))
            {
                argumentsDict[param.Name] = value;
            }
            else if (param.Required)
            {
                // If required parameter is missing, add null
                argumentsDict[param.Name] = null;
            }
        }

        // Serialize to JSON
        var jsonString = JsonSerializer.Serialize(argumentsDict);
        var binaryData = BinaryData.FromString(jsonString);

        // Create the tool call
        return new DevGPTChatToolCall(
            id: Guid.NewGuid().ToString(),
            functionName: tool.FunctionName,
            functionArguments: binaryData
        );
    }

    /// <summary>
    /// Get .NET type from JSON schema type string
    /// </summary>
    private Type GetParameterType(string jsonType)
    {
        return jsonType.ToLowerInvariant() switch
        {
            "string" => typeof(string),
            "number" => typeof(double),
            "integer" => typeof(int),
            "boolean" => typeof(bool),
            "array" => typeof(object[]),
            "object" => typeof(object),
            _ => typeof(string) // Default to string
        };
    }
}
