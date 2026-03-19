using Hazina.LLMs.Streaming;

namespace Hazina.LLMs.Client.Tests.Streaming;

public class StreamingToolCallAssemblerTests
{
    [Fact]
    public void AddChunk_WithValidFirstChunk_CreatesToolCall()
    {
        var assembler = new StreamingToolCallAssembler();

        var result = assembler.AddChunk("tool_1", "get_weather", "{\"location\":");

        Assert.NotNull(result);
        Assert.Equal("tool_1", result.ToolCallId);
        Assert.Equal("get_weather", result.FunctionName);
        Assert.Equal("{\"location\":", result.Arguments);
        Assert.False(result.IsComplete);
        Assert.Equal(1, result.ChunkCount);
    }

    [Fact]
    public void AddChunk_WithSubsequentChunks_AppendsArguments()
    {
        var assembler = new StreamingToolCallAssembler();

        assembler.AddChunk("tool_1", "get_weather", "{\"location\":");
        assembler.AddChunk("tool_1", null, "\"Paris\"");
        var result = assembler.AddChunk("tool_1", null, "}");

        Assert.NotNull(result);
        Assert.Equal("{\"location\":\"Paris\"}", result.Arguments);
        Assert.Equal(3, result.ChunkCount);
    }

    [Fact]
    public void AddChunk_WithMissingToolCallId_ReturnsError()
    {
        var assembler = new StreamingToolCallAssembler();

        var result = assembler.AddChunk(null, "get_weather", "{}");

        Assert.NotNull(result);
        Assert.Single(result.Errors);
        Assert.Contains("Tool call ID is missing", result.Errors[0]);
    }

    [Fact]
    public void AddChunk_WithMissingFunctionNameInFirstChunk_ReturnsError()
    {
        var assembler = new StreamingToolCallAssembler();

        var result = assembler.AddChunk("tool_1", null, "{}");

        Assert.NotNull(result);
        Assert.Single(result.Errors);
        Assert.Contains("function name", result.Errors[0].ToLowerInvariant());
    }

    [Fact]
    public void CompleteToolCall_WithValidJson_Succeeds()
    {
        var assembler = new StreamingToolCallAssembler();

        assembler.AddChunk("tool_1", "get_weather", "{\"location\":\"Paris\"}");
        var result = assembler.CompleteToolCall("tool_1");

        Assert.NotNull(result);
        Assert.True(result.IsComplete);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void CompleteToolCall_WithInvalidJson_RecordsError()
    {
        var assembler = new StreamingToolCallAssembler();

        assembler.AddChunk("tool_1", "get_weather", "{invalid json");
        var result = assembler.CompleteToolCall("tool_1");

        Assert.NotNull(result);
        Assert.True(result.IsComplete);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("Invalid JSON", result.Errors[0]);
    }

    [Fact]
    public void CompleteToolCall_WithEmptyArguments_DefaultsToEmptyObject()
    {
        var assembler = new StreamingToolCallAssembler();

        assembler.AddChunk("tool_1", "list_files", null);
        var result = assembler.CompleteToolCall("tool_1");

        Assert.NotNull(result);
        Assert.Equal("{}", result.Arguments);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void GetCompletedToolCalls_ReturnsOnlyCompletedCalls()
    {
        var assembler = new StreamingToolCallAssembler();

        assembler.AddChunk("tool_1", "func1", "{}");
        assembler.CompleteToolCall("tool_1");

        assembler.AddChunk("tool_2", "func2", "{");
        // tool_2 not completed

        var completed = assembler.GetCompletedToolCalls().ToList();

        Assert.Single(completed);
        Assert.Equal("tool_1", completed[0].ToolCallId);
    }

    [Fact]
    public void GetInProgressToolCalls_ReturnsOnlyInProgressCalls()
    {
        var assembler = new StreamingToolCallAssembler();

        assembler.AddChunk("tool_1", "func1", "{}");
        assembler.CompleteToolCall("tool_1");

        assembler.AddChunk("tool_2", "func2", "{");
        // tool_2 not completed

        var inProgress = assembler.GetInProgressToolCalls().ToList();

        Assert.Single(inProgress);
        Assert.Equal("tool_2", inProgress[0].ToolCallId);
    }

    [Fact]
    public void AddChunk_WithChangedFunctionName_RecordsError()
    {
        var assembler = new StreamingToolCallAssembler();

        assembler.AddChunk("tool_1", "get_weather", "{");
        var result = assembler.AddChunk("tool_1", "get_temperature", "}");

        Assert.NotNull(result);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("Function name changed", result.Errors[0]);
    }

    [Fact]
    public void Clear_RemovesAllToolCalls()
    {
        var assembler = new StreamingToolCallAssembler();

        assembler.AddChunk("tool_1", "func1", "{}");
        assembler.AddChunk("tool_2", "func2", "{}");

        assembler.Clear();

        Assert.Empty(assembler.GetCompletedToolCalls());
        Assert.Empty(assembler.GetInProgressToolCalls());
    }

    [Fact]
    public void GetDiagnostics_ReturnsDetailedInformation()
    {
        var assembler = new StreamingToolCallAssembler();

        assembler.AddChunk("tool_1", "func1", "{}");
        assembler.CompleteToolCall("tool_1");

        assembler.AddChunk("tool_2", "func2", "{invalid");

        var diagnostics = assembler.GetDiagnostics();

        Assert.Contains("Total tool calls: 2", diagnostics);
        Assert.Contains("Completed: 1", diagnostics);
        Assert.Contains("In progress: 1", diagnostics);
    }
}
