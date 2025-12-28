using Hazina.LLMs.GoogleADK.Tools.Registry;
using Xunit;

namespace Hazina.LLMs.GoogleADK.Tests.Tools;

public class ToolRegistryTests
{
    [Fact]
    public void RegisterTool_ShouldAddToolToRegistry()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = CreateSampleTool("test_tool");

        // Act
        registry.RegisterTool(tool);

        // Assert
        var retrievedTool = registry.GetTool("test_tool");
        Assert.NotNull(retrievedTool);
        Assert.Equal("test_tool", retrievedTool.FunctionName);
    }

    [Fact]
    public void UnregisterTool_ShouldRemoveToolFromRegistry()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = CreateSampleTool("test_tool");
        registry.RegisterTool(tool);

        // Act
        var result = registry.UnregisterTool("test_tool");

        // Assert
        Assert.True(result);
        var retrievedTool = registry.GetTool("test_tool");
        Assert.Null(retrievedTool);
    }

    [Fact]
    public void GetAllTools_ShouldReturnAllRegisteredTools()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.RegisterTool(CreateSampleTool("tool1"));
        registry.RegisterTool(CreateSampleTool("tool2"));
        registry.RegisterTool(CreateSampleTool("tool3"));

        // Act
        var tools = registry.GetAllTools();

        // Assert
        Assert.Equal(3, tools.Count);
    }

    [Fact]
    public void GetToolsByCategory_ShouldReturnToolsInCategory()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.RegisterTool(CreateSampleTool("tool1"), new ToolMetadata { Name = "tool1", Category = "data" });
        registry.RegisterTool(CreateSampleTool("tool2"), new ToolMetadata { Name = "tool2", Category = "data" });
        registry.RegisterTool(CreateSampleTool("tool3"), new ToolMetadata { Name = "tool3", Category = "search" });

        // Act
        var dataTools = registry.GetToolsByCategory("data");

        // Assert
        Assert.Equal(2, dataTools.Count);
        Assert.All(dataTools, tool => Assert.Contains(tool.FunctionName, new[] { "tool1", "tool2" }));
    }

    [Fact]
    public void SearchTools_ShouldFindToolsByNameOrDescription()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.RegisterTool(CreateSampleTool("search_web", "Search the web for information"));
        registry.RegisterTool(CreateSampleTool("read_file", "Read a file from disk"));
        registry.RegisterTool(CreateSampleTool("web_scraper", "Scrape data from websites"));

        // Act
        var results = registry.SearchTools("web");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, tool =>
            Assert.True(
                tool.FunctionName.Contains("web", StringComparison.OrdinalIgnoreCase) ||
                tool.Description.Contains("web", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void GetStatistics_ShouldReturnCorrectCounts()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.RegisterTool(CreateSampleTool("tool1"), new ToolMetadata { Name = "tool1", Category = "data" });
        registry.RegisterTool(CreateSampleTool("tool2"), new ToolMetadata { Name = "tool2", Category = "search" });

        // Act
        var stats = registry.GetStatistics();

        // Assert
        Assert.Equal(2, stats.TotalTools);
        Assert.Equal(2, stats.ToolsByCategory.Count);
        Assert.Equal(1, stats.ToolsByCategory["data"]);
        Assert.Equal(1, stats.ToolsByCategory["search"]);
    }

    private HazinaChatTool CreateSampleTool(string name, string description = "Test tool")
    {
        return new HazinaChatTool(
            name,
            description,
            new List<ChatToolParameter>(),
            async (messages, call, ct) => await Task.FromResult("success")
        );
    }
}
