using FluentAssertions;

namespace Hazina.LLMs.Helpers.Tests;

public class TokenCounterTests
{
    private readonly TokenCounter _tokenCounter;

    public TokenCounterTests()
    {
        _tokenCounter = new TokenCounter();
    }

    #region CountTokens Tests

    [Fact]
    public void CountTokens_WithEmptyString_ReturnsZero()
    {
        // Arrange
        var text = "";

        // Act
        var result = _tokenCounter.CountTokens(text);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CountTokens_WithNull_ReturnsZero()
    {
        // Arrange
        string? text = null;

        // Act
        var result = _tokenCounter.CountTokens(text!);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CountTokens_WithWhitespaceOnly_ReturnsZero()
    {
        // Arrange
        var text = "   \n\t  ";

        // Act
        var result = _tokenCounter.CountTokens(text);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CountTokens_WithSimpleText_ReturnsPositiveCount()
    {
        // Arrange
        var text = "Hello, world!";

        // Act
        var result = _tokenCounter.CountTokens(text);

        // Assert
        result.Should().BeGreaterThan(0);
        // "Hello, world!" should be approximately 4 tokens
        result.Should().BeInRange(2, 6);
    }

    [Fact]
    public void CountTokens_WithLongerText_ReturnsHigherCount()
    {
        // Arrange
        var shortText = "Hello";
        var longText = "Hello, this is a much longer piece of text with many more words.";

        // Act
        var shortResult = _tokenCounter.CountTokens(shortText);
        var longResult = _tokenCounter.CountTokens(longText);

        // Assert
        longResult.Should().BeGreaterThan(shortResult);
    }

    [Fact]
    public void CountTokens_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var text = "Special chars: @#$%^&*()";

        // Act
        var result = _tokenCounter.CountTokens(text);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CountTokens_WithUnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        var text = "Unicode: 你好世界 😊";

        // Act
        var result = _tokenCounter.CountTokens(text);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CountTokens_WithNewlines_CountsCorrectly()
    {
        // Arrange
        var text = "Line 1\nLine 2\nLine 3";

        // Act
        var result = _tokenCounter.CountTokens(text);

        // Assert
        result.Should().BeGreaterThan(0);
        // Should count tokens across all lines
        result.Should().BeGreaterThan(5);
    }

    [Fact]
    public void CountTokens_IsConsistent_ReturnsSameValueForSameInput()
    {
        // Arrange
        var text = "Consistency test text";

        // Act
        var result1 = _tokenCounter.CountTokens(text);
        var result2 = _tokenCounter.CountTokens(text);

        // Assert
        result1.Should().Be(result2);
    }

    #endregion

    #region CountTotalTokens Tests

    [Fact]
    public void CountTotalTokens_WithEmptyCollection_ReturnsZero()
    {
        // Arrange
        var documents = new List<string>();

        // Act
        var result = _tokenCounter.CountTotalTokens(documents);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CountTotalTokens_WithNullCollection_ReturnsZero()
    {
        // Arrange
        List<string>? documents = null;

        // Act
        var result = _tokenCounter.CountTotalTokens(documents!);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CountTotalTokens_WithSingleDocument_ReturnsCorrectCount()
    {
        // Arrange
        var documents = new List<string> { "This is a document." };

        // Act
        var result = _tokenCounter.CountTotalTokens(documents);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CountTotalTokens_WithMultipleDocuments_ReturnsSumOfAllTokens()
    {
        // Arrange
        var documents = new List<string>
        {
            "Document one.",
            "Document two.",
            "Document three."
        };

        var individualCounts = documents.Select(d => _tokenCounter.CountTokens(d)).ToList();
        var expectedTotal = individualCounts.Sum();

        // Act
        var result = _tokenCounter.CountTotalTokens(documents);

        // Assert
        result.Should().Be(expectedTotal);
    }

    [Fact]
    public void CountTotalTokens_WithEmptyDocuments_IgnoresEmptyOnes()
    {
        // Arrange
        var documents = new List<string>
        {
            "Document with content.",
            "",
            "Another document.",
            "   "
        };

        // Act
        var result = _tokenCounter.CountTotalTokens(documents);

        // Assert
        // Should only count non-empty documents
        var doc1Tokens = _tokenCounter.CountTokens("Document with content.");
        var doc2Tokens = _tokenCounter.CountTokens("Another document.");
        result.Should().Be(doc1Tokens + doc2Tokens);
    }

    #endregion

    #region AnalyzeTokens Tests

    [Fact]
    public void AnalyzeTokens_WithEmptyString_ReturnsZeroCountAndEmptyPreview()
    {
        // Arrange
        var text = "";

        // Act
        var (tokenCount, tokenPreview) = _tokenCounter.AnalyzeTokens(text);

        // Assert
        tokenCount.Should().Be(0);
        tokenPreview.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeTokens_WithSimpleText_ReturnsCountAndPreview()
    {
        // Arrange
        var text = "Hello, this is a test.";

        // Act
        var (tokenCount, tokenPreview) = _tokenCounter.AnalyzeTokens(text);

        // Assert
        tokenCount.Should().BeGreaterThan(0);
        tokenPreview.Should().NotBeEmpty();
    }

    [Fact]
    public void AnalyzeTokens_WithLongText_TruncatesPreview()
    {
        // Arrange
        var text = string.Join(" ", Enumerable.Range(0, 100).Select(i => $"word{i}"));

        // Act
        var (tokenCount, tokenPreview) = _tokenCounter.AnalyzeTokens(text);

        // Assert
        tokenCount.Should().BeGreaterThan(10);
        tokenPreview.Should().Contain("...");
    }

    [Fact]
    public void AnalyzeTokens_WithShortText_DoesNotTruncatePreview()
    {
        // Arrange
        var text = "Short text";

        // Act
        var (tokenCount, tokenPreview) = _tokenCounter.AnalyzeTokens(text);

        // Assert
        tokenPreview.Should().NotContain("...");
    }

    #endregion

    #region FilterDocumentsByTokenLimit Tests

    [Fact]
    public void FilterDocumentsByTokenLimit_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var documents = new List<string>();
        var maxTokens = 1000;

        // Act
        var result = _tokenCounter.FilterDocumentsByTokenLimit(documents, maxTokens);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FilterDocumentsByTokenLimit_WhenAllDocumentsFit_ReturnsAllDocuments()
    {
        // Arrange
        var documents = new List<string>
        {
            "Short doc 1",
            "Short doc 2",
            "Short doc 3"
        };
        var maxTokens = 10000;

        // Act
        var result = _tokenCounter.FilterDocumentsByTokenLimit(documents, maxTokens);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(documents);
    }

    [Fact]
    public void FilterDocumentsByTokenLimit_WhenSomeDocumentsFit_ReturnsSubset()
    {
        // Arrange
        var documents = new List<string>
        {
            "Document one with some content.",
            "Document two with more content.",
            "Document three with even more content for testing."
        };
        var maxTokens = 20; // Only first one or two should fit

        // Act
        var result = _tokenCounter.FilterDocumentsByTokenLimit(documents, maxTokens);

        // Assert
        result.Should().HaveCountLessThan(documents.Count);
        // Should include first document
        result.Should().Contain(documents[0]);
    }

    [Fact]
    public void FilterDocumentsByTokenLimit_StopsAtExactLimit_ReturnsCorrectSubset()
    {
        // Arrange
        var documents = new List<string>
        {
            "Small",
            "Medium size doc",
            "Large document with lots of content"
        };

        var firstTwoTokens = _tokenCounter.CountTokens(documents[0]) +
                            _tokenCounter.CountTokens(documents[1]);
        var maxTokens = firstTwoTokens;

        // Act
        var result = _tokenCounter.FilterDocumentsByTokenLimit(documents, maxTokens);

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().Be(documents[0]);
        result[1].Should().Be(documents[1]);
    }

    [Fact]
    public void FilterDocumentsByTokenLimit_WithZeroLimit_ReturnsEmptyList()
    {
        // Arrange
        var documents = new List<string> { "Document" };
        var maxTokens = 0;

        // Act
        var result = _tokenCounter.FilterDocumentsByTokenLimit(documents, maxTokens);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FilterDocumentsByTokenLimit_WithNegativeLimit_ReturnsEmptyList()
    {
        // Arrange
        var documents = new List<string> { "Document" };
        var maxTokens = -100;

        // Act
        var result = _tokenCounter.FilterDocumentsByTokenLimit(documents, maxTokens);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FilterDocumentsByTokenLimit_PreservesOrder()
    {
        // Arrange
        var documents = new List<string>
        {
            "First",
            "Second",
            "Third",
            "Fourth"
        };
        var maxTokens = 1000;

        // Act
        var result = _tokenCounter.FilterDocumentsByTokenLimit(documents, maxTokens);

        // Assert
        for (int i = 0; i < result.Count - 1; i++)
        {
            var originalIndex1 = documents.IndexOf(result[i]);
            var originalIndex2 = documents.IndexOf(result[i + 1]);
            originalIndex1.Should().BeLessThan(originalIndex2);
        }
    }

    [Fact]
    public void FilterDocumentsByTokenLimit_WithSingleLargeDocument_ReturnsEmptyIfTooLarge()
    {
        // Arrange
        var largeDoc = string.Join(" ", Enumerable.Range(0, 1000).Select(i => $"word{i}"));
        var documents = new List<string> { largeDoc };
        var maxTokens = 10; // Much smaller than the document

        // Act
        var result = _tokenCounter.FilterDocumentsByTokenLimit(documents, maxTokens);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Static Encoder Tests

    [Fact]
    public void TokenCounter_UsesStaticEncoder_ForEfficiency()
    {
        // Arrange
        var counter1 = new TokenCounter();
        var counter2 = new TokenCounter();
        var text = "Test text";

        // Act
        var result1 = counter1.CountTokens(text);
        var result2 = counter2.CountTokens(text);

        // Assert
        // Both counters should return the same result (using same static encoder)
        result1.Should().Be(result2);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void CountTokens_WithExtremelyLongText_HandlesGracefully()
    {
        // Arrange
        var veryLongText = new string('a', 1000000); // 1 million characters

        // Act
        Action act = () => _tokenCounter.CountTokens(veryLongText);

        // Assert
        act.Should().NotThrow();
    }

    #endregion
}
