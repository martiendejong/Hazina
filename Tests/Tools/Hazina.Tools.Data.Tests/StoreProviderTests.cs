using Hazina.GenerationTools.Data;
using FluentAssertions;

namespace Hazina.GenerationTools.Data.Tests;

public class StoreProviderTests
{
    [Fact]
    public void GetStoreSetup_WithNullFolder_ShouldThrow()
    {
        // Arrange & Act
        Action act = () => StoreProvider.GetStoreSetup(null!, "test-api-key");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetStoreSetup_WithEmptyApiKey_ShouldThrow()
    {
        // Arrange
        var tempFolder = Path.GetTempPath();

        // Act
        Action act = () => StoreProvider.GetStoreSetup(tempFolder, "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetStoreSetup_WithValidParameters_ShouldReturnStoreSetup()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempFolder);

        try
        {
            // Act
            var setup = StoreProvider.GetStoreSetup(tempFolder, "test-api-key");

            // Assert
            setup.Should().NotBeNull();
            setup.Store.Should().NotBeNull();
            setup.LLMClient.Should().NotBeNull();
            setup.TextStore.Should().NotBeNull();
            setup.DocumentPartStore.Should().NotBeNull();
            setup.TextEmbeddingStore.Should().NotBeNull();
        }
        finally
        {
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);
        }
    }
}
