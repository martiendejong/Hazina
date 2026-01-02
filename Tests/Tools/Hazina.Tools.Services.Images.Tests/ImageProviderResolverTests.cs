using Hazina.Tools.Services.Images.Abstractions;
using Hazina.Tools.Services.Images.Operations;
using Hazina.Tools.Services.Images.Providers;
using Moq;

namespace Hazina.Tools.Services.Images.Tests;

public class ImageProviderResolverTests
{
    [Fact]
    public void Resolve_WithSupportedOperation_ReturnsProvider()
    {
        // Arrange
        var mockProvider = new Mock<IImageProvider>();
        mockProvider.Setup(p => p.Name).Returns("Test");
        mockProvider.Setup(p => p.CanHandle(It.IsAny<CropOperation>())).Returns(true);

        var resolver = new ImageProviderResolver(new[] { mockProvider.Object });
        var operation = new CropOperation(0, 0, 100, 100);
        var options = new ImageProviderOptions();

        // Act
        var result = resolver.Resolve(operation, options);

        // Assert
        result.Should().Be(mockProvider.Object);
    }

    [Fact]
    public void Resolve_WithUnsupportedOperation_ThrowsException()
    {
        // Arrange
        var mockProvider = new Mock<IImageProvider>();
        mockProvider.Setup(p => p.Name).Returns("Test");
        mockProvider.Setup(p => p.CanHandle(It.IsAny<ImageEditOperation>())).Returns(false);

        var resolver = new ImageProviderResolver(new[] { mockProvider.Object });
        var operation = new CropOperation(0, 0, 100, 100);
        var options = new ImageProviderOptions();

        // Act & Assert
        var act = () => resolver.Resolve(operation, options);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No provider found*");
    }

    [Fact]
    public void Resolve_WithPreferredProvider_SelectsPreferred()
    {
        // Arrange
        var localProvider = new Mock<IImageProvider>();
        localProvider.Setup(p => p.Name).Returns("Local");
        localProvider.Setup(p => p.CanHandle(It.IsAny<CropOperation>())).Returns(true);

        var aiProvider = new Mock<IImageProvider>();
        aiProvider.Setup(p => p.Name).Returns("AI");
        aiProvider.Setup(p => p.CanHandle(It.IsAny<CropOperation>())).Returns(true);

        var resolver = new ImageProviderResolver(new[] { localProvider.Object, aiProvider.Object });
        var operation = new CropOperation(0, 0, 100, 100);
        var options = new ImageProviderOptions { PreferredProvider = "AI" };

        // Act
        var result = resolver.Resolve(operation, options);

        // Assert
        result.Should().Be(aiProvider.Object);
    }

    [Fact]
    public void Resolve_WithPreferredProviderThatCannotHandle_FallsBackToOther()
    {
        // Arrange
        var localProvider = new Mock<IImageProvider>();
        localProvider.Setup(p => p.Name).Returns("Local");
        localProvider.Setup(p => p.CanHandle(It.IsAny<CropOperation>())).Returns(true);

        var aiProvider = new Mock<IImageProvider>();
        aiProvider.Setup(p => p.Name).Returns("AI");
        aiProvider.Setup(p => p.CanHandle(It.IsAny<CropOperation>())).Returns(false);

        var resolver = new ImageProviderResolver(new[] { localProvider.Object, aiProvider.Object });
        var operation = new CropOperation(0, 0, 100, 100);
        var options = new ImageProviderOptions { PreferredProvider = "AI" };

        // Act
        var result = resolver.Resolve(operation, options);

        // Assert
        result.Should().Be(localProvider.Object);
    }

    [Fact]
    public void GetAllProviders_ReturnsAllRegisteredProviders()
    {
        // Arrange
        var provider1 = new Mock<IImageProvider>();
        var provider2 = new Mock<IImageProvider>();
        var resolver = new ImageProviderResolver(new[] { provider1.Object, provider2.Object });

        // Act
        var providers = resolver.GetAllProviders();

        // Assert
        providers.Should().HaveCount(2);
        providers.Should().Contain(provider1.Object);
        providers.Should().Contain(provider2.Object);
    }

    [Fact]
    public void Resolve_WithNoProviders_ThrowsException()
    {
        // Arrange
        var resolver = new ImageProviderResolver(Array.Empty<IImageProvider>());
        var operation = new CropOperation(0, 0, 100, 100);
        var options = new ImageProviderOptions();

        // Act & Assert
        var act = () => resolver.Resolve(operation, options);
        act.Should().Throw<InvalidOperationException>();
    }
}
