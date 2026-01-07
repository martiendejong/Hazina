using FluentAssertions;
using Hazina.Tools.Models;
using Hazina.Tools.Services.ToolAgent.ToolsContexts;
using System.Text.Json;

namespace Hazina.Tools.Services.ToolAgent.Tests;

public class ToolAgentToolsContextTests
{
    [Fact]
    public void Constructor_RegistersExpectedTools()
    {
        // Arrange & Act
        var context = new ToolAgentToolsContext("test-project", "test-chat", "test-user");

        // Assert
        context.Tools.Should().HaveCount(5);
        context.Tools.Should().Contain(t => t.FunctionName == "GetAnalysisFields");
        context.Tools.Should().Contain(t => t.FunctionName == "TriggerAnalysisFieldGeneration");
        context.Tools.Should().Contain(t => t.FunctionName == "TriggerImageGeneration");
        context.Tools.Should().Contain(t => t.FunctionName == "StoreGatheredData");
        context.Tools.Should().Contain(t => t.FunctionName == "ShowGuidanceCard");
    }

    [Fact]
    public async Task GetAnalysisFields_ReturnsMetadata()
    {
        // Arrange
        var context = new ToolAgentToolsContext("test-project", "test-chat");
        var tool = context.Tools.First(t => t.FunctionName == "GetAnalysisFields");
        var toolCall = new HazinaChatToolCall(
            "test-id",
            "GetAnalysisFields",
            BinaryData.FromString("{}")
        );

        // Act
        var result = await tool.Execute(new List<HazinaChatMessage>(), toolCall, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("fields").GetArrayLength().Should().BeGreaterThan(0);
        context.ExecutedTools.Should().Contain("GetAnalysisFields");
    }

    [Fact]
    public async Task TriggerAnalysisFieldGeneration_WithFieldKey_ReturnsSuccess()
    {
        // Arrange
        var context = new ToolAgentToolsContext("test-project", "test-chat");
        var tool = context.Tools.First(t => t.FunctionName == "TriggerAnalysisFieldGeneration");
        var toolCall = new HazinaChatToolCall(
            "test-id",
            "TriggerAnalysisFieldGeneration",
            BinaryData.FromString(JsonSerializer.Serialize(new
            {
                field_key = "brand-profile",
                instruction = "Focus on Italian cuisine"
            }))
        );

        // Act
        var result = await tool.Execute(new List<HazinaChatMessage>(), toolCall, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("field_key").GetString().Should().Be("brand-profile");
        context.ExecutedTools.Should().Contain("TriggerAnalysisFieldGeneration:brand-profile");
    }

    [Fact]
    public async Task TriggerAnalysisFieldGeneration_WithoutFieldKey_ReturnsError()
    {
        // Arrange
        var context = new ToolAgentToolsContext("test-project", "test-chat");
        var tool = context.Tools.First(t => t.FunctionName == "TriggerAnalysisFieldGeneration");
        var toolCall = new HazinaChatToolCall(
            "test-id",
            "TriggerAnalysisFieldGeneration",
            BinaryData.FromString("{}")
        );

        // Act
        var result = await tool.Execute(new List<HazinaChatMessage>(), toolCall, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Contain("field_key");
    }

    [Fact]
    public async Task TriggerImageGeneration_WithImageType_ReturnsSuccess()
    {
        // Arrange
        var context = new ToolAgentToolsContext("test-project", "test-chat");
        var tool = context.Tools.First(t => t.FunctionName == "TriggerImageGeneration");
        var toolCall = new HazinaChatToolCall(
            "test-id",
            "TriggerImageGeneration",
            BinaryData.FromString(JsonSerializer.Serialize(new { image_type = "logo" }))
        );

        // Act
        var result = await tool.Execute(new List<HazinaChatMessage>(), toolCall, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        context.ExecutedTools.Should().Contain("TriggerImageGeneration:logo");
    }

    [Fact]
    public async Task StoreGatheredData_WithKeyAndValue_ReturnsSuccess()
    {
        // Arrange
        var context = new ToolAgentToolsContext("test-project", "test-chat");
        var tool = context.Tools.First(t => t.FunctionName == "StoreGatheredData");
        var toolCall = new HazinaChatToolCall(
            "test-id",
            "StoreGatheredData",
            BinaryData.FromString(JsonSerializer.Serialize(new
            {
                key = "brand-name",
                value = "Bella Italia",
                title = "Brand Name"
            }))
        );

        // Act
        var result = await tool.Execute(new List<HazinaChatMessage>(), toolCall, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("key").GetString().Should().Be("brand-name");
        context.ExecutedTools.Should().Contain("StoreGatheredData:brand-name");
    }

    [Fact]
    public async Task ShowGuidanceCard_WithTypeAndMessage_ReturnsSuccess()
    {
        // Arrange
        var context = new ToolAgentToolsContext("test-project", "test-chat");
        var tool = context.Tools.First(t => t.FunctionName == "ShowGuidanceCard");
        var toolCall = new HazinaChatToolCall(
            "test-id",
            "ShowGuidanceCard",
            BinaryData.FromString(JsonSerializer.Serialize(new
            {
                card_type = "question",
                message = "Tell me about your business"
            }))
        );

        // Act
        var result = await tool.Execute(new List<HazinaChatMessage>(), toolCall, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("card_type").GetString().Should().Be("question");
        context.ExecutedTools.Should().Contain("ShowGuidanceCard:question");
    }

    [Fact]
    public void ExecutedTools_TracksAllToolCalls()
    {
        // Arrange
        var context = new ToolAgentToolsContext("test-project", "test-chat");

        // Act - Execute multiple tools
        var getFieldsTool = context.Tools.First(t => t.FunctionName == "GetAnalysisFields");
        getFieldsTool.Execute(
            new List<HazinaChatMessage>(),
            new HazinaChatToolCall("test-id", "GetAnalysisFields", BinaryData.FromString("{}")),
            CancellationToken.None).Wait();

        var triggerTool = context.Tools.First(t => t.FunctionName == "TriggerAnalysisFieldGeneration");
        triggerTool.Execute(
            new List<HazinaChatMessage>(),
            new HazinaChatToolCall(
                "test-id",
                "TriggerAnalysisFieldGeneration",
                BinaryData.FromString(JsonSerializer.Serialize(new { field_key = "mission" }))
            ),
            CancellationToken.None).Wait();

        // Assert
        context.ExecutedTools.Should().HaveCount(2);
        context.ExecutedTools.Should().Contain("GetAnalysisFields");
        context.ExecutedTools.Should().Contain("TriggerAnalysisFieldGeneration:mission");
    }

    [Theory]
    [InlineData("brand-profile")]
    [InlineData("mission")]
    [InlineData("vision")]
    [InlineData("core-values")]
    public async Task TriggerAnalysisFieldGeneration_WithDifferentFields_ReturnsSuccess(string fieldKey)
    {
        // Arrange
        var context = new ToolAgentToolsContext("test-project", "test-chat");
        var tool = context.Tools.First(t => t.FunctionName == "TriggerAnalysisFieldGeneration");
        var toolCall = new HazinaChatToolCall(
            "test-id",
            "TriggerAnalysisFieldGeneration",
            BinaryData.FromString(JsonSerializer.Serialize(new { field_key = fieldKey }))
        );

        // Act
        var result = await tool.Execute(new List<HazinaChatMessage>(), toolCall, CancellationToken.None);

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("field_key").GetString().Should().Be(fieldKey);
    }
}
