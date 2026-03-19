using FluentAssertions;

namespace Hazina.LLMs.Helpers.Tests;

public class DocumentSplitterTests
{
    [Fact]
    public void SplitDocument_WithEmptyString_ReturnsEmptyList()
    {
        // Arrange
        var splitter = new DocumentSplitter { TokensPerPart = 100 };
        var content = "";

        // Act
        var result = splitter.SplitDocument(content);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SplitDocument_WithShortContent_ReturnsSinglePart()
    {
        // Arrange
        var splitter = new DocumentSplitter { TokensPerPart = 1000 };
        var content = "This is a short document.";

        // Act
        var result = splitter.SplitDocument(content);

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().Be(content);
    }

    [Fact]
    public void SplitDocument_WithLongContent_SplitsIntoMultipleParts()
    {
        // Arrange
        var splitter = new DocumentSplitter { TokensPerPart = 10 };
        var content = string.Join("\n", Enumerable.Range(0, 100).Select(i => "This is line " + i));

        // Act
        var result = splitter.SplitDocument(content);

        // Assert
        result.Should().HaveCountGreaterThan(1);
        // All parts should be non-empty
        result.Should().AllSatisfy(part => part.Should().NotBeEmpty());
    }

    [Fact]
    public void SplitDocument_WithCustomSeparator_UsesCorrectSeparator()
    {
        // Arrange
        var splitter = new DocumentSplitter { TokensPerPart = 100 };
        var content = "Part1|Part2|Part3";

        // Act
        var result = splitter.SplitDocument(content, "|");

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void SplitDocument_WithCustomSeparator_PreservesSeparatorInOutput()
    {
        // Arrange
        var splitter = new DocumentSplitter { TokensPerPart = 100 };
        var content = "Line1\nLine2\nLine3";

        // Act
        var result = splitter.SplitDocument(content, "\n");

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().Contain("\n");
    }

    [Fact]
    public void SplitDocument_RespectsSplitBoundaries()
    {
        // Arrange
        var splitter = new DocumentSplitter { TokensPerPart = 50 };
        var lines = Enumerable.Range(0, 20).Select(i => $"Line {i}: This is some content");
        var content = string.Join("\n", lines);

        // Act
        var result = splitter.SplitDocument(content, "\n");

        // Assert
        result.Should().HaveCountGreaterThan(1);
        // Each part should only contain complete lines
        foreach (var part in result)
        {
            var partLines = part.Split("\n");
            partLines.Should().AllSatisfy(line =>
            {
                if (!string.IsNullOrEmpty(line))
                {
                    line.Should().StartWith("Line ");
                }
            });
        }
    }

    [Fact]
    public void SplitFile_WithExistingFile_ReadsAndSplits()
    {
        // Arrange
        var splitter = new DocumentSplitter { TokensPerPart = 100 };
        var tempFile = Path.GetTempFileName();
        var content = "This is test content\nWith multiple lines\nFor testing purposes";
        File.WriteAllText(tempFile, content);

        try
        {
            // Act
            var result = splitter.SplitFile(tempFile);

            // Assert
            result.Should().NotBeEmpty();
            string.Join("\n", result).Should().Be(content);
        }
        finally
        {
            // Cleanup
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void SplitFile_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var splitter = new DocumentSplitter { TokensPerPart = 100 };
        var nonExistentFile = "C:/this/file/does/not/exist.txt";

        // Act & Assert
        Action act = () => splitter.SplitFile(nonExistentFile);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void TokensPerPart_DefaultValue_Is1000()
    {
        // Arrange & Act
        var splitter = new DocumentSplitter();

        // Assert
        splitter.TokensPerPart.Should().Be(1000);
    }

    [Fact]
    public void TokensPerPart_CanBeModified()
    {
        // Arrange
        var splitter = new DocumentSplitter();

        // Act
        splitter.TokensPerPart = 500;

        // Assert
        splitter.TokensPerPart.Should().Be(500);
    }
}

public class StringSplitterTests
{
    [Fact]
    public void Split_WithCommaSeparatedString_ReturnsArray()
    {
        // Arrange
        var input = "apple,banana,cherry";

        // Act
        var result = StringSplitter.Split(input);

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().Be("apple");
        result[1].Should().Be("banana");
        result[2].Should().Be("cherry");
    }

    [Fact]
    public void Split_WithSpacesAroundCommas_TrimsWhitespace()
    {
        // Arrange
        var input = "apple , banana , cherry";

        // Act
        var result = StringSplitter.Split(input);

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().Be("apple");
        result[1].Should().Be("banana");
        result[2].Should().Be("cherry");
    }

    [Fact]
    public void Split_WithSingleItem_ReturnsSingleElementArray()
    {
        // Arrange
        var input = "apple";

        // Act
        var result = StringSplitter.Split(input);

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().Be("apple");
    }

    [Fact]
    public void Split_WithEmptyString_ReturnsSingleEmptyElement()
    {
        // Arrange
        var input = "";

        // Act
        var result = StringSplitter.Split(input);

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().Be("");
    }

    [Fact]
    public void Split_WithMultipleConsecutiveCommas_ReturnsEmptyElements()
    {
        // Arrange
        var input = "apple,,cherry";

        // Act
        var result = StringSplitter.Split(input);

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().Be("apple");
        result[1].Should().Be("");
        result[2].Should().Be("cherry");
    }
}
