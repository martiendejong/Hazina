using Xunit;
using FluentAssertions;
using Hazina.App.HazinaCoder.Core.Security;

namespace Hazina.App.HazinaCoder.Tests.Core.Security;

public class SecretScannerTests
{
    [Theory]
    [InlineData("sk-abcd1234efgh5678ijkl9012mnop3456qrst7890uvwx1234yz56", true)]  // OpenAI
    [InlineData("just some regular text", false)]
    [InlineData("api_key = 'sk-test123'", true)]
    [InlineData("ghp_1234567890123456789012345678901234567890", true)]  // GitHub PAT
    public void ScanForSecrets_ShouldDetect_KnownPatterns(string content, bool shouldDetect)
    {
        // Arrange
        var scanner = new SecretScanner();

        // Act
        var secrets = scanner.Scan(content);

        // Assert
        if (shouldDetect)
            secrets.Should().NotBeEmpty();
        else
            secrets.Should().BeEmpty();
    }

    [Fact]
    public void ScanForSecrets_WithHighEntropy_ShouldDetect()
    {
        // Arrange
        var scanner = new SecretScanner();
        var highEntropyString = "x9aK2mP7qR4nL8vB3wE6yU1tI5oA0sD";

        // Act
        var secrets = scanner.Scan(highEntropyString);

        // Assert
        secrets.Should().NotBeEmpty();
        secrets.First().Type.Should().Be("high-entropy");
    }

    [Fact]
    public void ScanForSecrets_WithLowEntropy_ShouldNotDetect()
    {
        // Arrange
        var scanner = new SecretScanner();
        var lowEntropyString = "aaaaaaaaaaaaaaaa";

        // Act
        var secrets = scanner.Scan(lowEntropyString);

        // Assert
        secrets.Should().BeEmpty();
    }

    [Fact]
    public void ScanFile_WithSecrets_ShouldReturnLineNumbers()
    {
        // Arrange
        var scanner = new SecretScanner();
        var tempFile = Path.GetTempFileName();

        File.WriteAllLines(tempFile, new[]
        {
            "Line 1: normal content",
            "Line 2: sk-abcd1234efgh5678ijkl9012mnop3456qrst7890uvwx1234yz56",
            "Line 3: more normal content"
        });

        try
        {
            // Act
            var secrets = scanner.ScanFile(tempFile);

            // Assert
            secrets.Should().NotBeEmpty();
            secrets.First().LineNumber.Should().Be(2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void AddCustomPattern_ShouldDetect_NewPattern()
    {
        // Arrange
        var scanner = new SecretScanner();
        scanner.AddPattern("custom", "SECRET-[0-9]{6}");

        // Act
        var secrets = scanner.Scan("Here is SECRET-123456 in text");

        // Assert
        secrets.Should().NotBeEmpty();
        secrets.First().Type.Should().Be("custom");
    }
}
