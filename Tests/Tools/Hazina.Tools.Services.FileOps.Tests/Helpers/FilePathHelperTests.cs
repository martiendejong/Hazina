using Hazina.GenerationTools.Services.FileOps.Helpers;
using FluentAssertions;

namespace Hazina.GenerationTools.Services.FileOps.Tests.Helpers;

public class FilePathHelperTests
{
    [Fact]
    public void GetFileExtension_WithStandardFile_ShouldReturnExtension()
    {
        // Arrange
        var fileName = "document.pdf";

        // Act
        var extension = FilePathHelper.GetFileExtension(fileName);

        // Assert
        extension.Should().Be("pdf");
    }

    [Fact]
    public void GetFileExtension_WithNoExtension_ShouldReturnTxt()
    {
        // Arrange
        var fileName = "document";

        // Act
        var extension = FilePathHelper.GetFileExtension(fileName);

        // Assert
        extension.Should().Be("txt");
    }

    [Fact]
    public void GetFileExtension_WithEmptyString_ShouldReturnTxt()
    {
        // Act
        var extension = FilePathHelper.GetFileExtension("");

        // Assert
        extension.Should().Be("txt");
    }

    [Fact]
    public void GetTextFilePath_WithValidPath_ShouldReturnCorrectFormat()
    {
        // Arrange
        var uploadedFilePath = Path.Combine("folder", "document.pdf");

        // Act
        var textFilePath = FilePathHelper.GetTextFilePath(uploadedFilePath);

        // Assert
        textFilePath.Should().Contain("document.pdf.txt");
    }

    [Fact]
    public void GetTextFilePath_WithEmptyPath_ShouldThrow()
    {
        // Act
        Action act = () => FilePathHelper.GetTextFilePath("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("uploadedFilePath");
    }

    [Fact]
    public void GetUploadsFolder_WithValidPath_ShouldReturnCorrectPath()
    {
        // Arrange
        var projectPath = "C:\\Projects\\MyProject";

        // Act
        var uploadsFolder = FilePathHelper.GetUploadsFolder(projectPath);

        // Assert
        uploadsFolder.Should().EndWith("Uploads");
        uploadsFolder.Should().Contain(projectPath);
    }

    [Fact]
    public void GetUploadsFolder_WithEmptyPath_ShouldThrow()
    {
        // Act
        Action act = () => FilePathHelper.GetUploadsFolder("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("projectPath");
    }
}
