namespace DevGPT.LLMs.Classes.Tests;

public class LLMResponseTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        // Arrange
        var result = "Hello, world!";
        var tokenUsage = new TokenUsageInfo(10, 5, 0.001m, 0.0005m, "gpt-4o");

        // Act
        var response = new LLMResponse<string>(result, tokenUsage);

        // Assert
        Assert.Equal(result, response.Result);
        Assert.Equal(tokenUsage, response.TokenUsage);
    }

    [Fact]
    public void WithComplexType_ShouldWorkCorrectly()
    {
        // Arrange
        var result = new TestResponse { Message = "Test", Success = true };
        var tokenUsage = new TokenUsageInfo(20, 10, 0.002m, 0.001m, "claude-3-5-sonnet");

        // Act
        var response = new LLMResponse<TestResponse>(result, tokenUsage);

        // Assert
        Assert.Equal("Test", response.Result.Message);
        Assert.True(response.Result.Success);
        Assert.Equal(30, response.TokenUsage.TotalTokens);
    }

    [Fact]
    public void WithNullableResult_ShouldHandleNull()
    {
        // Arrange
        var tokenUsage = new TokenUsageInfo(10, 5, 0.001m, 0.0005m, "gpt-4o");

        // Act
        var response = new LLMResponse<TestResponse?>(null, tokenUsage);

        // Assert
        Assert.Null(response.Result);
        Assert.Equal(15, response.TokenUsage.TotalTokens);
    }

    private class TestResponse
    {
        public string Message { get; set; } = string.Empty;
        public bool Success { get; set; }
    }
}
