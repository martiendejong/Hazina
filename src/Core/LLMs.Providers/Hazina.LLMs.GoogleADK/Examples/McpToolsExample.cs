using Hazina.LLMs.GoogleADK.Agents;
using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Client;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Transport;
using Hazina.LLMs.GoogleADK.Tools.Registry;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Examples;

/// <summary>
/// Example demonstrating how to use MCP tools with agents
/// </summary>
public class McpToolsExample
{
    /// <summary>
    /// Example 1: Create an MCP agent that connects to a stdio MCP server
    /// </summary>
    public static async Task StdioMcpAgentExample(ILLMClient llmClient, ILogger logger)
    {
        // Create tool registry
        var toolRegistry = new ToolRegistry(logger);

        // Create MCP agent
        var agent = new McpAgent(
            name: "MCP Assistant",
            llmClient: llmClient,
            toolRegistry: toolRegistry,
            context: new AgentContext(new AgentState(), new Events.EventBus(), logger)
        );

        // Connect to an MCP server via stdio
        // This could be a Node.js MCP server, Python MCP server, etc.
        await agent.ConnectToStdioServerAsync(
            serverCommand: "node",
            serverArgs: new[] { "path/to/mcp-server.js" },
            providerName: "FileSystemTools",
            category: "filesystem"
        );

        // Initialize and execute
        await agent.InitializeAsync();

        var result = await agent.ExecuteAsync("List all files in the current directory");

        Console.WriteLine($"Result: {result.Output}");

        await agent.DisposeAsync();
    }

    /// <summary>
    /// Example 2: Create an MCP agent that connects to an HTTP MCP server
    /// </summary>
    public static async Task HttpMcpAgentExample(ILLMClient llmClient, ILogger logger)
    {
        var toolRegistry = new ToolRegistry(logger);

        var agent = new McpAgent(
            name: "Web MCP Assistant",
            llmClient: llmClient,
            toolRegistry: toolRegistry,
            context: new AgentContext(new AgentState(), new Events.EventBus(), logger)
        );

        // Connect to HTTP MCP server
        await agent.ConnectToHttpServerAsync(
            serverUrl: "http://localhost:3000",
            providerName: "WebAPITools",
            category: "api"
        );

        await agent.InitializeAsync();

        var result = await agent.ExecuteAsync("Fetch the weather for San Francisco");

        Console.WriteLine($"Weather: {result.Output}");

        await agent.DisposeAsync();
    }

    /// <summary>
    /// Example 3: Create a custom MCP client and add it to an agent
    /// </summary>
    public static async Task CustomMcpClientExample(ILLMClient llmClient, ILogger logger)
    {
        // Create custom MCP transport
        var transport = new StdioMcpTransport(
            serverCommand: "python",
            serverArgs: new[] { "mcp_server.py" },
            logger: logger
        );

        // Create MCP client
        var mcpClient = new McpClient(
            transport: transport,
            clientName: "CustomClient",
            clientVersion: "1.0.0",
            logger: logger
        );

        await mcpClient.InitializeAsync();

        // Create agent and add the client
        var toolRegistry = new ToolRegistry(logger);
        var agent = new McpAgent(
            name: "Custom MCP Agent",
            llmClient: llmClient,
            toolRegistry: toolRegistry,
            context: new AgentContext(new AgentState(), new Events.EventBus(), logger)
        );

        await agent.AddMcpClientAsync(
            client: mcpClient,
            providerName: "CustomTools",
            category: "custom"
        );

        await agent.InitializeAsync();

        // List available tools
        var stats = agent.GetToolStatistics();
        Console.WriteLine($"Total tools: {stats.TotalTools}");

        // Search for specific tools
        var searchResults = agent.SearchTools("file");
        Console.WriteLine($"Found {searchResults.Count} tools matching 'file'");

        await agent.DisposeAsync();
    }

    /// <summary>
    /// Example 4: Use ToolEnabledAgent with manual tool registration
    /// </summary>
    public static async Task ToolEnabledAgentExample(ILLMClient llmClient, ILogger logger)
    {
        var toolRegistry = new ToolRegistry(logger);

        // Create a custom tool
        var calculatorTool = new HazinaChatTool(
            name: "calculator",
            description: "Performs basic arithmetic operations",
            parameters: new List<ChatToolParameter>
            {
                new ChatToolParameter
                {
                    Name = "operation",
                    Type = "string",
                    Description = "The operation: add, subtract, multiply, divide",
                    Required = true
                },
                new ChatToolParameter
                {
                    Name = "a",
                    Type = "number",
                    Description = "First number",
                    Required = true
                },
                new ChatToolParameter
                {
                    Name = "b",
                    Type = "number",
                    Description = "Second number",
                    Required = true
                }
            },
            execute: (messages, toolCall, cancellationToken) =>
            {
                var argumentsJson = toolCall.FunctionArguments.ToString();
                var args = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson);
                if (args == null) return Task.FromResult("Error: Invalid arguments");

                var operation = args["operation"].ToString();
                var a = Convert.ToDouble(args["a"]);
                var b = Convert.ToDouble(args["b"]);

                var result = operation switch
                {
                    "add" => a + b,
                    "subtract" => a - b,
                    "multiply" => a * b,
                    "divide" => b != 0 ? a / b : double.NaN,
                    _ => double.NaN
                };

                return Task.FromResult($"Result: {result}");
            }
        );

        // Create agent
        var agent = new ToolEnabledAgent(
            name: "Calculator Agent",
            llmClient: llmClient,
            toolRegistry: toolRegistry,
            context: new AgentContext(new AgentState(), new Events.EventBus(), logger),
            autoDiscoverTools: false
        );

        // Add tool
        agent.AddTool(calculatorTool, new ToolMetadata
        {
            Name = "calculator",
            Description = "Basic calculator",
            Category = "math",
            Tags = new List<string> { "arithmetic", "computation" }
        });

        await agent.InitializeAsync();

        var result = await agent.ExecuteAsync("What is 25 multiplied by 4?");

        Console.WriteLine($"Answer: {result.Output}");

        await agent.DisposeAsync();
    }

    /// <summary>
    /// Example 5: Connect to multiple MCP servers
    /// </summary>
    public static async Task MultipleServerExample(ILLMClient llmClient, ILogger logger)
    {
        var toolRegistry = new ToolRegistry(logger);

        var agent = new McpAgent(
            name: "Multi-Server Agent",
            llmClient: llmClient,
            toolRegistry: toolRegistry,
            context: new AgentContext(new AgentState(), new Events.EventBus(), logger)
        );

        // Connect to filesystem tools
        await agent.ConnectToStdioServerAsync(
            serverCommand: "node",
            serverArgs: new[] { "filesystem-mcp-server.js" },
            providerName: "FileSystem",
            category: "filesystem"
        );

        // Connect to database tools
        await agent.ConnectToStdioServerAsync(
            serverCommand: "python",
            serverArgs: new[] { "database-mcp-server.py" },
            providerName: "Database",
            category: "database"
        );

        // Connect to web API tools
        await agent.ConnectToHttpServerAsync(
            serverUrl: "http://localhost:3000",
            providerName: "WebAPI",
            category: "api"
        );

        await agent.InitializeAsync();

        // Get statistics
        var stats = agent.GetToolStatistics();
        Console.WriteLine($"Total tools from all servers: {stats.TotalTools}");
        Console.WriteLine("Tools by category:");
        foreach (var category in stats.ToolsByCategory)
        {
            Console.WriteLine($"  {category.Key}: {category.Value}");
        }

        // Get tools by category
        var fileTools = agent.GetToolsByCategory("filesystem");
        Console.WriteLine($"\nFilesystem tools: {fileTools.Count}");

        // Use the agent
        var result = await agent.ExecuteAsync(
            "Read the contents of config.json and store it in the database"
        );

        Console.WriteLine($"\nResult: {result.Output}");

        await agent.DisposeAsync();
    }
}
