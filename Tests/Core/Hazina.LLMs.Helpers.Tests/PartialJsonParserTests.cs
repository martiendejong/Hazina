using FluentAssertions;
using System.Text.Json;

namespace Hazina.LLMs.Helpers.Tests;

public class PartialJsonParserTests
{
    private readonly PartialJsonParser _parser;

    public PartialJsonParserTests()
    {
        _parser = new PartialJsonParser();
    }

    #region CountBraces Tests

    [Fact]
    public void CountBraces_WithEmptyString_ReturnsZeroBraces()
    {
        // Arrange
        var input = "";

        // Act
        var (openBraces, closeBraces) = PartialJsonParser.CountBraces(input);

        // Assert
        openBraces.Should().Be(0);
        closeBraces.Should().Be(0);
    }

    [Fact]
    public void CountBraces_WithBalancedBraces_ReturnsCorrectCount()
    {
        // Arrange
        var input = "{\"name\": \"test\"}";

        // Act
        var (openBraces, closeBraces) = PartialJsonParser.CountBraces(input);

        // Assert
        openBraces.Should().Be(1);
        closeBraces.Should().Be(1);
    }

    [Fact]
    public void CountBraces_WithNestedBraces_ReturnsCorrectCount()
    {
        // Arrange
        var input = "{\"outer\": {\"inner\": {}}}";

        // Act
        var (openBraces, closeBraces) = PartialJsonParser.CountBraces(input);

        // Assert
        openBraces.Should().Be(3);
        closeBraces.Should().Be(3);
    }

    [Fact]
    public void CountBraces_WithUnbalancedBraces_ReturnsCorrectCount()
    {
        // Arrange
        var input = "{\"incomplete\": {\"nested\":";

        // Act
        var (openBraces, closeBraces) = PartialJsonParser.CountBraces(input);

        // Assert
        openBraces.Should().Be(2);
        closeBraces.Should().Be(0);
    }

    [Fact]
    public void CountBraces_WithOnlyClosingBraces_ReturnsCorrectCount()
    {
        // Arrange
        var input = "}}}";

        // Act
        var (openBraces, closeBraces) = PartialJsonParser.CountBraces(input);

        // Assert
        openBraces.Should().Be(0);
        closeBraces.Should().Be(3);
    }

    [Fact]
    public void CountBraces_WithMixedBraces_ReturnsCorrectCount()
    {
        // Arrange
        var input = "{{{}}}{{";

        // Act
        var (openBraces, closeBraces) = PartialJsonParser.CountBraces(input);

        // Assert
        openBraces.Should().Be(5);
        closeBraces.Should().Be(3);
    }

    #endregion

    #region Parse Valid JSON Tests

    [Fact]
    public void Parse_WithValidSimpleObject_ReturnsDeserializedObject()
    {
        // Arrange
        var json = "{\"name\":\"John\",\"age\":30}";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void Parse_WithValidNestedObject_ReturnsDeserializedObject()
    {
        // Arrange
        var json = "{\"name\":\"John\",\"address\":{\"city\":\"NYC\",\"zip\":\"10001\"}}";

        // Act
        var result = _parser.Parse<TestPersonWithAddress>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
        result.Address.Should().NotBeNull();
        result.Address!.City.Should().Be("NYC");
        result.Address.Zip.Should().Be("10001");
    }

    [Fact]
    public void Parse_WithValidArray_ReturnsDeserializedArray()
    {
        // Arrange
        var json = "[{\"name\":\"John\"},{\"name\":\"Jane\"}]";

        // Act
        var result = _parser.Parse<List<TestPerson>>(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result![0].Name.Should().Be("John");
        result[1].Name.Should().Be("Jane");
    }

    #endregion

    #region Parse JSON with Prefix Tests

    [Fact]
    public void Parse_WithTextBeforeObject_RemovesPrefixAndParses()
    {
        // Arrange
        var json = "Some text before {\"name\":\"John\",\"age\":30}";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void Parse_WithTextBeforeArray_RemovesPrefixAndParses()
    {
        // Arrange
        var json = "Here is the data: [{\"name\":\"John\"}]";

        // Act
        var result = _parser.Parse<List<TestPerson>>(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].Name.Should().Be("John");
    }

    [Fact]
    public void Parse_WithMultilinePrefixBeforeObject_RemovesPrefixAndParses()
    {
        // Arrange
        var json = @"Line 1
Line 2
Line 3
{""name"":""John""}";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
    }

    [Fact]
    public void Parse_WithObjectBeforeArray_ParsesFirstStructure()
    {
        // Arrange - object comes before array
        var json = "{\"name\":\"John\"} [{\"name\":\"Jane\"}]";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
    }

    [Fact]
    public void Parse_WithArrayBeforeObject_ParsesFirstStructure()
    {
        // Arrange - array comes before object
        var json = "[{\"name\":\"Jane\"}] {\"name\":\"John\"}";

        // Act
        var result = _parser.Parse<List<TestPerson>>(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].Name.Should().Be("Jane");
    }

    #endregion

    #region Parse Malformed JSON Tests

    [Fact]
    public void Parse_WithMissingClosingBrace_AddsClosingBrace()
    {
        // Arrange
        var json = "{\"name\":\"John\",\"age\":30";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void Parse_WithMissingClosingBracket_AddsClosingBracket()
    {
        // Arrange
        var json = "[{\"name\":\"John\"},{\"name\":\"Jane\"";

        // Act
        var result = _parser.Parse<List<TestPerson>>(json);

        // Assert
        result.Should().NotBeNull();
        // At least one item should be parsed
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_WithDoubledOpenBraces_RemovesDuplicates()
    {
        // Arrange
        var json = "{{\"name\":\"John\"}}";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
    }

    [Fact]
    public void Parse_WithDoubledCloseBraces_RemovesDuplicates()
    {
        // Arrange
        var json = "{\"name\":\"John\"}}";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
    }

    [Fact]
    public void Parse_WithTrailingComma_HandlesGracefully()
    {
        // Arrange
        var json = "{\"name\":\"John\",\"age\":30,}";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        // Should either parse successfully or handle gracefully
        // JSON with trailing comma is technically invalid but common
        result.Should().NotBeNull();
    }

    [Fact]
    public void Parse_WithMissingQuotes_ThrowsOrReturnsNull()
    {
        // Arrange
        var json = "{name:John}";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        // Invalid JSON without quotes should be handled
        // Either returns null or throws exception based on implementation
        result.Should().BeNull();
    }

    #endregion

    #region Parse Edge Cases Tests

    [Fact]
    public void Parse_WithEmptyObject_ReturnsEmptyObject()
    {
        // Arrange
        var json = "{}";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().BeNull();
        result.Age.Should().Be(0);
    }

    [Fact]
    public void Parse_WithEmptyArray_ReturnsEmptyArray()
    {
        // Arrange
        var json = "[]";

        // Act
        var result = _parser.Parse<List<TestPerson>>(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithNullValues_HandlesNullsCorrectly()
    {
        // Arrange
        var json = "{\"name\":null,\"age\":30}";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().BeNull();
        result.Age.Should().Be(30);
    }

    [Fact]
    public void Parse_WithSpecialCharactersInStrings_HandlesCorrectly()
    {
        // Arrange
        var json = "{\"name\":\"John\\nDoe\\t\",\"age\":30}";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("John\nDoe\t");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void Parse_WithUnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        var json = "{\"name\":\"\\u0048\\u0065\\u006C\\u006C\\u006F\",\"age\":30}";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Hello");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void Parse_WithVeryLongString_HandlesCorrectly()
    {
        // Arrange
        var longName = new string('A', 10000);
        var json = $"{{\"name\":\"{longName}\",\"age\":30}}";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(longName);
        result.Age.Should().Be(30);
    }

    [Fact]
    public void Parse_WithDeeplyNestedObject_HandlesCorrectly()
    {
        // Arrange
        var json = "{\"level1\":{\"level2\":{\"level3\":{\"level4\":{\"value\":\"deep\"}}}}}";

        // Act
        var result = _parser.Parse<DeepNestedObject>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Level1.Should().NotBeNull();
        result.Level1!.Level2.Should().NotBeNull();
        result.Level1.Level2!.Level3.Should().NotBeNull();
        result.Level1.Level2.Level3!.Level4.Should().NotBeNull();
        result.Level1.Level2.Level3.Level4!.Value.Should().Be("deep");
    }

    [Fact]
    public void Parse_WithLargeArray_HandlesCorrectly()
    {
        // Arrange
        var items = string.Join(",", Enumerable.Range(0, 1000).Select(i => $"{{\"name\":\"Person{i}\"}}"));
        var json = $"[{items}]";

        // Act
        var result = _parser.Parse<List<TestPerson>>(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1000);
        result![0].Name.Should().Be("Person0");
        result[999].Name.Should().Be("Person999");
    }

    [Fact]
    public void Parse_WithNoJsonStructure_ReturnsNull()
    {
        // Arrange
        var json = "This is not JSON at all";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_WithOnlyWhitespace_ReturnsNull()
    {
        // Arrange
        var json = "   \n\t  ";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Parse Streaming/Partial JSON Tests

    [Fact]
    public void Parse_WithIncompleteStreamingObject_AttemptsToFixAndParse()
    {
        // Arrange - simulating a streaming response that was cut off
        var json = "{\"name\":\"John\",\"age\":30,\"address\":{\"city\":\"NYC\"";

        // Act
        var result = _parser.Parse<TestPersonWithAddress>(json);

        // Assert
        // Should attempt to close braces and parse what's available
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void Parse_WithPartialStringValue_HandlesGracefully()
    {
        // Arrange - string value cut off mid-stream
        var json = "{\"name\":\"Jo";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        // Should handle gracefully, either by closing or returning null
        // Implementation determines exact behavior
        result.Should().NotBeNull();
    }

    [Fact]
    public void Parse_WithMultipleIncompleteObjects_ParsesFirst()
    {
        // Arrange
        var json = "{\"name\":\"John\"} {\"name\":\"Ja";

        // Act
        var result = _parser.Parse<TestPerson>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
    }

    #endregion

    #region Helper Classes

    private class TestPerson
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    private class TestPersonWithAddress
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public Address? Address { get; set; }
    }

    private class Address
    {
        public string? City { get; set; }
        public string? Zip { get; set; }
    }

    private class DeepNestedObject
    {
        public Level1? Level1 { get; set; }
    }

    private class Level1
    {
        public Level2? Level2 { get; set; }
    }

    private class Level2
    {
        public Level3? Level3 { get; set; }
    }

    private class Level3
    {
        public Level4? Level4 { get; set; }
    }

    private class Level4
    {
        public string? Value { get; set; }
    }

    #endregion
}
