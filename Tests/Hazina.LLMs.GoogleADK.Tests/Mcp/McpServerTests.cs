using System.Text.Json;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Models;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Server;
using Xunit;

namespace Hazina.LLMs.GoogleADK.Tests.Mcp;

public class McpServerTests
{
    [Fact]
    public async Task HandleRequest_Initialize_ShouldReturnServerInfo()
    {
        // Arrange
        var server = new McpServer("TestServer", "1.0.0");
        var request = new McpRequest
        {
            Method = "initialize",
            Params = new { }
        };

        // Act
        var response = await server.HandleRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Null(response.Error);
        Assert.NotNull(response.Result);
    }

    [Fact]
    public async Task RegisterTool_AndListTools_ShouldReturnRegisteredTool()
    {
        // Arrange
        var server = new McpServer();
        var tool = new McpTool
        {
            Name = "test_tool",
            Description = "A test tool",
            InputSchema = JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}")
        };

        server.RegisterTool(tool, (args, ct) =>
        {
            return Task.FromResult(new CallToolResponse
            {
                Content = new List<ToolContent>
                {
                    new ToolContent { Type = "text", Text = "Success" }
                }
            });
        });

        var request = new McpRequest
        {
            Method = "tools/list",
            Params = new ListToolsRequest()
        };

        // Act
        var response = await server.HandleRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Null(response.Error);

        var result = JsonSerializer.Deserialize<ListToolsResponse>(
            JsonSerializer.SerializeToElement(response.Result));

        Assert.NotNull(result);
        Assert.Single(result.Tools);
        Assert.Equal("test_tool", result.Tools[0].Name);
    }

    [Fact]
    public async Task CallTool_RegisteredTool_ShouldExecute()
    {
        // Arrange
        var server = new McpServer();
        var executed = false;

        var tool = new McpTool
        {
            Name = "test_tool",
            Description = "A test tool",
            InputSchema = JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}")
        };

        server.RegisterTool(tool, (args, ct) =>
        {
            executed = true;
            return Task.FromResult(new CallToolResponse
            {
                Content = new List<ToolContent>
                {
                    new ToolContent { Type = "text", Text = "Executed" }
                }
            });
        });

        var request = new McpRequest
        {
            Method = "tools/call",
            Params = new CallToolRequest
            {
                Name = "test_tool",
                Arguments = new Dictionary<string, object>()
            }
        };

        // Act
        var response = await server.HandleRequestAsync(request);

        // Assert
        Assert.True(executed);
        Assert.NotNull(response);
        Assert.Null(response.Error);
    }

    [Fact]
    public async Task CallTool_UnknownTool_ShouldReturnError()
    {
        // Arrange
        var server = new McpServer();
        var request = new McpRequest
        {
            Method = "tools/call",
            Params = new CallToolRequest
            {
                Name = "unknown_tool",
                Arguments = new Dictionary<string, object>()
            }
        };

        // Act
        var response = await server.HandleRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Error);
        Assert.Contains("not found", response.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetStatistics_ShouldReturnCorrectCounts()
    {
        // Arrange
        var server = new McpServer();

        var tool = new McpTool
        {
            Name = "tool1",
            Description = "Tool 1",
            InputSchema = JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}")
        };

        server.RegisterTool(tool, (args, ct) => Task.FromResult(new CallToolResponse()));

        var resource = new McpResource
        {
            Uri = "resource://test",
            Name = "Test Resource"
        };

        server.RegisterResource(resource, ct => Task.FromResult(new ReadResourceResponse()));

        // Act
        var stats = server.GetStatistics();

        // Assert
        Assert.Equal(1, stats.ToolCount);
        Assert.Equal(1, stats.ResourceCount);
        Assert.Equal(0, stats.PromptCount);
    }
}
