using Hazina.LLMs.GoogleADK.Tools.Validation;
using Xunit;

namespace Hazina.LLMs.GoogleADK.Tests.Tools;

public class ToolValidatorTests
{
    [Fact]
    public void ValidateToolDefinition_ValidTool_ShouldPass()
    {
        // Arrange
        var validator = new ToolValidator();
        var tool = new HazinaChatTool(
            "valid_tool",
            "This is a valid tool with a good description",
            new List<ChatToolParameter>
            {
                new ChatToolParameter
                {
                    Name = "param1",
                    Type = "string",
                    Description = "First parameter",
                    Required = true
                }
            },
            async (messages, call, ct) => await Task.FromResult("success")
        );

        // Act
        var result = validator.ValidateToolDefinition(tool);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateToolDefinition_EmptyName_ShouldFail()
    {
        // Arrange
        var validator = new ToolValidator();
        var tool = new HazinaChatTool(
            "",
            "Description",
            new List<ChatToolParameter>(),
            async (messages, call, ct) => await Task.FromResult("success")
        );

        // Act
        var result = validator.ValidateToolDefinition(tool);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name cannot be empty"));
    }

    [Fact]
    public void ValidateToolDefinition_InvalidName_ShouldFail()
    {
        // Arrange
        var validator = new ToolValidator();
        var tool = new HazinaChatTool(
            "invalid tool name!",
            "Description",
            new List<ChatToolParameter>(),
            async (messages, call, ct) => await Task.FromResult("success")
        );

        // Act
        var result = validator.ValidateToolDefinition(tool);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("invalid"));
    }

    [Fact]
    public void ValidateToolDefinition_DuplicateParameters_ShouldFail()
    {
        // Arrange
        var validator = new ToolValidator();
        var tool = new HazinaChatTool(
            "test_tool",
            "Test tool",
            new List<ChatToolParameter>
            {
                new ChatToolParameter { Name = "param1", Type = "string" },
                new ChatToolParameter { Name = "param1", Type = "string" }
            },
            async (messages, call, ct) => await Task.FromResult("success")
        );

        // Act
        var result = validator.ValidateToolDefinition(tool);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate parameter"));
    }

    [Fact]
    public void ValidateArguments_AllRequiredPresent_ShouldPass()
    {
        // Arrange
        var validator = new ToolValidator();
        var parameters = new List<ChatToolParameter>
        {
            new ChatToolParameter { Name = "param1", Type = "string", Required = true },
            new ChatToolParameter { Name = "param2", Type = "number", Required = false }
        };
        var arguments = new Dictionary<string, object>
        {
            ["param1"] = "value1"
        };

        // Act
        var result = validator.ValidateArguments(parameters, arguments);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateArguments_MissingRequired_ShouldFail()
    {
        // Arrange
        var validator = new ToolValidator();
        var parameters = new List<ChatToolParameter>
        {
            new ChatToolParameter { Name = "param1", Type = "string", Required = true }
        };
        var arguments = new Dictionary<string, object>();

        // Act
        var result = validator.ValidateArguments(parameters, arguments);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Required parameter"));
    }

    [Fact]
    public void ValidateArguments_WrongType_ShouldFail()
    {
        // Arrange
        var validator = new ToolValidator();
        var parameters = new List<ChatToolParameter>
        {
            new ChatToolParameter { Name = "param1", Type = "number", Required = true }
        };
        var arguments = new Dictionary<string, object>
        {
            ["param1"] = "not a number"
        };

        // Act
        var result = validator.ValidateArguments(parameters, arguments);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("invalid type"));
    }
}
