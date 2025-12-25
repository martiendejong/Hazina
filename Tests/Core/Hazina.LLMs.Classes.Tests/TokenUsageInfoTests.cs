namespace DevGPT.LLMs.Classes.Tests;

public class TokenUsageInfoTests
{
    [Fact]
    public void TotalTokens_ShouldSumInputAndOutput()
    {
        // Arrange
        var tokenUsage = new TokenUsageInfo(100, 50, 0.002m, 0.001m, "gpt-4o");

        // Act & Assert
        Assert.Equal(150, tokenUsage.TotalTokens);
    }

    [Fact]
    public void TotalCost_ShouldSumInputAndOutputCosts()
    {
        // Arrange
        var tokenUsage = new TokenUsageInfo(100, 50, 0.002m, 0.001m, "gpt-4o");

        // Act & Assert
        Assert.Equal(0.003m, tokenUsage.TotalCost);
    }

    [Fact]
    public void OperatorPlus_ShouldAggregateTokens()
    {
        // Arrange
        var usage1 = new TokenUsageInfo(100, 50, 0.002m, 0.001m, "gpt-4o");
        var usage2 = new TokenUsageInfo(200, 100, 0.004m, 0.002m, "gpt-4o");

        // Act
        var total = usage1 + usage2;

        // Assert
        Assert.Equal(300, total.InputTokens);
        Assert.Equal(150, total.OutputTokens);
        Assert.Equal(450, total.TotalTokens);
        Assert.Equal(0.006m, total.InputCost);
        Assert.Equal(0.003m, total.OutputCost);
        Assert.Equal(0.009m, total.TotalCost);
        Assert.Equal("gpt-4o", total.ModelName);
    }

    [Fact]
    public void OperatorPlus_WithDifferentModels_ShouldPreferFirstModel()
    {
        // Arrange
        var usage1 = new TokenUsageInfo(100, 50, 0.002m, 0.001m, "gpt-4o");
        var usage2 = new TokenUsageInfo(200, 100, 0.004m, 0.002m, "claude-3-5-sonnet");

        // Act
        var total = usage1 + usage2;

        // Assert
        Assert.Equal("gpt-4o", total.ModelName);
    }

    [Fact]
    public void OperatorPlus_WithEmptyFirstModel_ShouldUseSecondModel()
    {
        // Arrange
        var usage1 = new TokenUsageInfo(100, 50, 0.002m, 0.001m, "");
        var usage2 = new TokenUsageInfo(200, 100, 0.004m, 0.002m, "claude-3-5-sonnet");

        // Act
        var total = usage1 + usage2;

        // Assert
        Assert.Equal("claude-3-5-sonnet", total.ModelName);
    }

    [Fact]
    public void DefaultConstructor_ShouldInitializeToZero()
    {
        // Arrange & Act
        var tokenUsage = new TokenUsageInfo();

        // Assert
        Assert.Equal(0, tokenUsage.InputTokens);
        Assert.Equal(0, tokenUsage.OutputTokens);
        Assert.Equal(0, tokenUsage.TotalTokens);
        Assert.Equal(0m, tokenUsage.InputCost);
        Assert.Equal(0m, tokenUsage.OutputCost);
        Assert.Equal(0m, tokenUsage.TotalCost);
        Assert.Equal(string.Empty, tokenUsage.ModelName);
    }

    [Fact]
    public void MultipleAdditions_ShouldAccumulateCorrectly()
    {
        // Arrange
        var usage1 = new TokenUsageInfo(100, 50, 0.002m, 0.001m, "gpt-4o");
        var usage2 = new TokenUsageInfo(200, 100, 0.004m, 0.002m, "gpt-4o");
        var usage3 = new TokenUsageInfo(150, 75, 0.003m, 0.0015m, "gpt-4o");

        // Act
        var total = usage1 + usage2 + usage3;

        // Assert
        Assert.Equal(450, total.InputTokens);
        Assert.Equal(225, total.OutputTokens);
        Assert.Equal(675, total.TotalTokens);
        Assert.Equal(0.009m, total.InputCost);
        Assert.Equal(0.0045m, total.OutputCost);
        Assert.Equal(0.0135m, total.TotalCost);
    }
}
