using Hazina.LLMs;

namespace Hazina.LLMs.Helpers.Tests;

public class PartialJsonParserTests
{
    private readonly PartialJsonParser _parser = new();

    #region CountBraces Tests

    [Fact]
    public void CountBraces_EmptyString_ReturnsZeros()
    {
        // Arrange & Act
        var (open, close) = PartialJsonParser.CountBraces("");

        // Assert
        Assert.Equal(0, open);
        Assert.Equal(0, close);
    }

    [Fact]
    public void CountBraces_BalancedBraces_ReturnsCorrectCounts()
    {
        // Arrange & Act
        var (open, close) = PartialJsonParser.CountBraces("{\"key\": {\"nested\": \"value\"}}");

        // Assert
        Assert.Equal(2, open);
        Assert.Equal(2, close);
    }

    [Fact]
    public void CountBraces_UnbalancedBraces_ReturnsCorrectCounts()
    {
        // Arrange & Act
        var (open, close) = PartialJsonParser.CountBraces("{\"key\": {\"nested\": \"value\"");

        // Assert
        Assert.Equal(2, open);
        Assert.Equal(0, close);
    }

    #endregion

    #region Direct Parse Tests

    [Fact]
    public void Parse_ValidJson_ReturnsParsedObject()
    {
        // Arrange
        var json = "{\"name\": \"test\", \"value\": 42}";

        // Act
        var result = _parser.Parse<TestObject>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test", result.name);
        Assert.Equal(42, result.value);
    }

    [Fact]
    public void Parse_ValidArray_ReturnsParsedArray()
    {
        // Arrange
        var json = "[{\"name\": \"test1\", \"value\": 1}, {\"name\": \"test2\", \"value\": 2}]";

        // Act
        var result = _parser.Parse<List<TestObject>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("test1", result[0].name);
        Assert.Equal("test2", result[1].name);
    }

    #endregion

    #region Leading Garbage Tests

    [Fact]
    public void Parse_JsonWithLeadingText_RemovesLeadingGarbage()
    {
        // Arrange
        var json = "Some random text before {\"name\": \"test\", \"value\": 42}";

        // Act
        var result = _parser.Parse<TestObject>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test", result.name);
        Assert.Equal(42, result.value);
    }

    [Fact]
    public void Parse_ArrayWithLeadingText_RemovesLeadingGarbage()
    {
        // Arrange
        var json = "data: [{\"name\": \"test\", \"value\": 42}]";

        // Act
        var result = _parser.Parse<List<TestObject>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("test", result[0].Name);
    }

    [Fact]
    public void Parse_MultipleStartChars_UsesFirstOccurrence()
    {
        // Arrange
        var json = "text [{\"name\": \"array\", \"value\": 1}] {\"name\": \"object\", \"value\": 2}";

        // Act
        var result = _parser.Parse<List<TestObject>>(json);

        // Assert - should parse the array that comes first
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("array", result[0].name);
    }

    #endregion

    #region Trailing Garbage Tests

    [Fact]
    public void Parse_JsonWithTrailingText_RemovesTrailingGarbage()
    {
        // Arrange
        var json = "{\"name\": \"test\", \"value\": 42} and some more text";

        // Act
        var result = _parser.Parse<TestObject>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test", result.name);
        Assert.Equal(42, result.value);
    }

    [Fact]
    public void Parse_IncompleteJson_ParsesUpToLastClosingBrace()
    {
        // Arrange
        var json = "{\"name\": \"test\", \"value\": 42, \"incomplete";

        // Act
        var result = _parser.Parse<TestObject>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test", result.name);
        Assert.Equal(42, result.value);
    }

    #endregion

    #region Brace Balancing Tests

    [Fact]
    public void Parse_UnbalancedBraces_BalancesAndParses()
    {
        // Arrange - missing closing brace
        var json = "{\"name\": \"test\", \"value\": 42";

        // Act
        var result = _parser.Parse<TestObject>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test", result.name);
        Assert.Equal(42, result.value);
    }

    [Fact]
    public void Parse_NestedUnbalancedBraces_BalancesAndParses()
    {
        // Arrange
        var json = "{\"name\": \"test\", \"nested\": {\"value\": 42";

        // Act
        var result = _parser.Parse<NestedTestObject>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test", result.name);
        Assert.NotNull(result.Nested);
        Assert.Equal(42, result.Nested.Value);
    }

    [Fact]
    public void Parse_UnbalancedArray_BalancesAndParses()
    {
        // Arrange
        var json = "[{\"name\": \"test\", \"value\": 1}, {\"name\": \"test2\", \"value\": 2";

        // Act
        var result = _parser.Parse<List<TestObject>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    #endregion

    #region Quote Escaping Tests

    [Fact]
    public void Parse_UnescapedQuotesInValue_EscapesAndParses()
    {
        // Arrange
        var json = "{\"name\": \"test with \"quotes\" inside\", \"value\": 42}";

        // Act
        var result = _parser.Parse<TestObject>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("quotes", result.name);
    }

    #endregion

    #region Doubled Delimiter Tests

    [Fact]
    public void Parse_DoubledBraces_RemovesDoubledDelimiters()
    {
        // Arrange
        var json = "{{\"name\": \"test\", \"value\": 42}}";

        // Act
        var result = _parser.Parse<TestObject>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test", result.name);
        Assert.Equal(42, result.value);
    }

    [Fact]
    public void Parse_DoubledBrackets_RemovesDoubledDelimiters()
    {
        // Arrange
        var json = "[[{\"name\": \"test\", \"value\": 42}]]";

        // Act
        var result = _parser.Parse<List<TestObject>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Parse_EmptyObject_ReturnsDefault()
    {
        // Arrange
        var json = "{}";

        // Act
        var result = _parser.Parse<TestObject>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.name);
        Assert.Equal(0, result.value);
    }

    [Fact]
    public void Parse_EmptyArray_ReturnsEmptyList()
    {
        // Arrange
        var json = "[]";

        // Act
        var result = _parser.Parse<List<TestObject>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NoJsonAtAll_ReturnsDefault()
    {
        // Arrange
        var json = "This is not JSON at all";

        // Act
        var result = _parser.Parse<TestObject>(json);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Parse_OnlyBraces_ReturnsDefault()
    {
        // Arrange
        var json = "{}}}}}";

        // Act
        var result = _parser.Parse<TestObject>(json);

        // Assert
        Assert.NotNull(result); // Empty object is valid
    }

    [Fact]
    public void Parse_ComplexNested_ParsesCorrectly()
    {
        // Arrange
        var json = @"{
            ""name"": ""parent"",
            ""nested"": {
                ""value"": 42,
                ""description"": ""test""
            }
        }";

        // Act
        var result = _parser.Parse<NestedTestObject>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("parent", result.name);
        Assert.NotNull(result.nested);
        Assert.Equal(42, result.nested.value);
    }

    #endregion

    #region Streaming Scenarios

    [Fact]
    public void Parse_StreamingPartialObject_ParsesWhatExists()
    {
        // Arrange - simulates partial streaming response
        var json = "{\"name\": \"streaming\"";

        // Act
        var result = _parser.Parse<TestObject>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("streaming", result.name);
    }

    [Fact]
    public void Parse_StreamingWithPrefix_HandlesDataPrefix()
    {
        // Arrange - common SSE streaming format
        var json = "data: {\"name\": \"stream\", \"value\": 99}";

        // Act
        var result = _parser.Parse<TestObject>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("stream", result.name);
        Assert.Equal(99, result.value);
    }

    #endregion

    #region Test Helper Classes

    public class TestObject
    {
        public string? name { get; set; }
        public int value { get; set; }
        public string? description { get; set; }
    }

    public class NestedTestObject
    {
        public string? name { get; set; }
        public InnerObject? nested { get; set; }
    }

    public class InnerObject
    {
        public int value { get; set; }
        public string? description { get; set; }
    }

    #endregion
}
