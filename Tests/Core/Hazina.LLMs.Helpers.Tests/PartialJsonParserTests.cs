using Xunit;
using System.Collections.Generic;

namespace Hazina.LLMs.Helpers.Tests;

public class TestModel
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class PartialJsonParserTests
{
    private readonly PartialJsonParser _parser = new();

    #region Basic Valid JSON Tests

    [Fact]
    public void Parse_ValidCompleteObject_ReturnsCorrectObject()
    {
        var json = """{"name":"John","age":30}""";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Equal("John", result.Name);
        Assert.Equal(30, result.Age);
    }

    [Fact]
    public void Parse_ValidArray_ReturnsCorrectList()
    {
        var json = """["item1","item2","item3"]""";
        var result = _parser.Parse<List<string>>(json);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("item1", result[0]);
    }

    #endregion

    #region Special Characters in Strings

    [Fact]
    public void Parse_StringWithTabs_HandlesCorrectly()
    {
        var json = """{"name":"John\tDoe","age":30}""";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Equal("John\tDoe", result.Name);
    }

    [Fact]
    public void Parse_StringWithNewlines_HandlesCorrectly()
    {
        var json = """{"name":"John\nDoe","age":30}""";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Equal("John\nDoe", result.Name);
    }

    [Fact]
    public void Parse_StringWithEscapedQuotes_HandlesCorrectly()
    {
        var json = """{"name":"John \"The Boss\" Doe","age":30}""";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Equal("John \"The Boss\" Doe", result.Name);
    }

    [Fact]
    public void Parse_StringWithBackslash_HandlesCorrectly()
    {
        var json = """{"name":"C:\\Users\\John","age":30}""";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Equal("C:\\Users\\John", result.Name);
    }

    [Fact]
    public void Parse_StringWithCarriageReturn_HandlesCorrectly()
    {
        var json = """{"name":"Line1\r\nLine2","age":30}""";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Equal("Line1\r\nLine2", result.Name);
    }

    #endregion

    #region Trailing Commas

    [Fact]
    public void Parse_TrailingCommaInObject_HandlesGracefully()
    {
        var json = """{"name":"John","age":30,}""";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Equal("John", result.Name);
        Assert.Equal(30, result.Age);
    }

    [Fact]
    public void Parse_TrailingCommaInArray_HandlesGracefully()
    {
        var json = """["item1","item2",]""";
        var result = _parser.Parse<List<string>>(json);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    #endregion

    #region Doubled Open/Close Braces

    [Fact]
    public void Parse_DoubledOpenBraces_HandlesCorrectly()
    {
        var json = """{{{"name":"John","age":30}""";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Equal("John", result.Name);
    }

    [Fact]
    public void Parse_DoubledCloseBraces_HandlesCorrectly()
    {
        var json = """{"name":"John","age":30}}}""";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Equal("John", result.Name);
    }

    [Fact]
    public void Parse_DoubledOpenBrackets_HandlesCorrectly()
    {
        var json = """[[[["item1","item2"]""";
        var result = _parser.Parse<List<string>>(json);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_DoubledCloseBrackets_HandlesCorrectly()
    {
        var json = """["item1","item2"]]]]""";
        var result = _parser.Parse<List<string>>(json);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    #endregion

    #region Text Before Arrays/Objects

    [Fact]
    public void Parse_TextBeforeObject_HandlesCorrectly()
    {
        var json = """Some random text before the JSON {"name":"John","age":30}""";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Equal("John", result.Name);
    }

    [Fact]
    public void Parse_TextBeforeArray_HandlesCorrectly()
    {
        var json = """Response: ["item1","item2"]""";
        var result = _parser.Parse<List<string>>(json);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    #endregion

    #region Unmatched Braces

    [Fact]
    public void Parse_MissingClosingBrace_AddsClosingBrace()
    {
        var json = "{\"name\":\"John\",\"age\":30";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Equal("John", result.Name);
        Assert.Equal(30, result.Age);
    }

    [Fact]
    public void Parse_MissingClosingBracket_AddsClosingBracket()
    {
        var json = "[\"item1\",\"item2\"";
        var result = _parser.Parse<List<string>>(json);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_MultipleNestedMissingBraces_HandlesCorrectly()
    {
        var json = "{\"name\":\"John\",\"metadata\":{\"key\":\"value\"";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Equal("John", result.Name);
    }

    #endregion

    #region Incomplete JSON Streaming

    [Fact]
    public void Parse_IncompletePropertyValue_HandlesGracefully()
    {
        var json = "{\"name\":\"Joh";
        var result = _parser.Parse<TestModel>(json);

        // Should either return null or handle gracefully
        // Depending on implementation, this might return partial data
        Assert.True(result == null || result.Name == "Joh" || result.Name == null);
    }

    [Fact]
    public void Parse_IncompletePropertyName_HandlesGracefully()
    {
        var json = "{\"nam";
        var result = _parser.Parse<TestModel>(json);

        // Should return null or empty object
        Assert.True(result == null || result.Name == null);
    }

    [Fact]
    public void Parse_StreamingPartialObject_HandlesGracefully()
    {
        var json = "{\"name\":\"John\",\"age\":";
        var result = _parser.Parse<TestModel>(json);

        // Should handle partial value gracefully
        Assert.True(result == null || result.Name == "John");
    }

    #endregion

    #region Escape Sequence Handling

    [Fact]
    public void Parse_EscapeSequences_AllTypes_HandlesCorrectly()
    {
        var json = """{"description":"Line1\nLine2\tTabbed\r\nWindows\bBackspace\"Quote"}""";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Contains("\n", result.Description);
        Assert.Contains("\t", result.Description);
    }

    [Fact]
    public void Parse_UnicodeEscapeSequence_HandlesCorrectly()
    {
        var json = """{"name":"Caf\u00e9"}""";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Equal("Café", result.Name);
    }

    #endregion

    #region Mixed Edge Cases

    [Fact]
    public void Parse_TextBeforeWithMissingBrace_HandlesCorrectly()
    {
        var json = """Here is the data: {"name":"John","age":30""";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Equal("John", result.Name);
    }

    [Fact]
    public void Parse_DoubledBracesWithSpecialChars_HandlesCorrectly()
    {
        var json = """{{{"name":"John\nDoe","age":30}}}""";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Equal("John\nDoe", result.Name);
    }

    [Fact]
    public void Parse_ComplexNestedWithAllIssues_HandlesCorrectly()
    {
        var json = "Text before {{{\"name\":\"John \\\"Boss\\\"\\nDoe\",\"age\":30,\"tags\":[\"tag1\",\"tag2\",]";
        var result = _parser.Parse<TestModel>(json);

        Assert.NotNull(result);
        Assert.Contains("John", result.Name);
    }

    #endregion

    #region CountBraces Tests

    [Fact]
    public void CountBraces_ValidJson_ReturnsCorrectCounts()
    {
        var json = """{"name":"John","nested":{}}""";
        var (open, close) = PartialJsonParser.CountBraces(json);

        Assert.Equal(2, open);
        Assert.Equal(2, close);
    }

    [Fact]
    public void CountBraces_UnbalancedJson_ReturnsCorrectCounts()
    {
        var json = """{"name":"John","nested":{}""";
        var (open, close) = PartialJsonParser.CountBraces(json);

        Assert.Equal(2, open);
        Assert.Equal(1, close);
    }

    [Fact]
    public void CountBraces_BracesInStrings_ExcludesStringContent()
    {
        // Should only count structural braces, not braces inside strings
        var json = """{"name":"John {test}"}""";
        var (open, close) = PartialJsonParser.CountBraces(json);

        Assert.Equal(1, open);
        Assert.Equal(1, close);
    }

    #endregion
}
