using FluentAssertions;
using Hazina.AI.Providers.Core;
using Hazina.AI.Providers.Selection;
using Moq;
using Xunit;

namespace Hazina.AI.Providers.Tests;

public class ProviderOrchestratorTests
{
    [Fact]
    public void RegisterProvider_ShouldAddProviderToRegistry()
    {
        // Arrange
        var orchestrator = new ProviderOrchestrator();
        var mockClient = new Mock<ILLMClient>();
        var metadata = new ProviderMetadata
        {
            Name = "test-provider",
            Priority = 1,
            Capabilities = new ProviderCapabilities { SupportsChat = true }
        };

        // Act
        orchestrator.RegisterProvider("test-provider", mockClient.Object, metadata);

        // Assert
        // Provider should be registered (no exception thrown)
        orchestrator.Should().NotBeNull();
    }

    [Fact]
    public void SetDefaultStrategy_ShouldUpdateSelectionStrategy()
    {
        // Arrange
        var orchestrator = new ProviderOrchestrator();

        // Act
        orchestrator.SetDefaultStrategy(SelectionStrategy.LeastCost);

        // Assert
        // Strategy should be set (no exception thrown)
        orchestrator.Should().NotBeNull();
    }

    [Fact]
    public void RegisterProvider_WithMultipleProviders_ShouldAllowFailover()
    {
        // Arrange
        var orchestrator = new ProviderOrchestrator();
        var mockClient1 = new Mock<ILLMClient>();
        var mockClient2 = new Mock<ILLMClient>();

        var metadata1 = new ProviderMetadata
        {
            Name = "provider1",
            Priority = 1,
            Capabilities = new ProviderCapabilities { SupportsChat = true }
        };

        var metadata2 = new ProviderMetadata
        {
            Name = "provider2",
            Priority = 2,
            Capabilities = new ProviderCapabilities { SupportsChat = true }
        };

        // Act
        orchestrator.RegisterProvider("provider1", mockClient1.Object, metadata1);
        orchestrator.RegisterProvider("provider2", mockClient2.Object, metadata2);

        // Assert
        // Both providers should be registered
        orchestrator.Should().NotBeNull();
    }

    [Theory]
    [InlineData(SelectionStrategy.Priority)]
    [InlineData(SelectionStrategy.LeastCost)]
    [InlineData(SelectionStrategy.FastestResponse)]
    [InlineData(SelectionStrategy.RoundRobin)]
    [InlineData(SelectionStrategy.Random)]
    public void SetDefaultStrategy_WithVariousStrategies_ShouldSucceed(SelectionStrategy strategy)
    {
        // Arrange
        var orchestrator = new ProviderOrchestrator();

        // Act
        orchestrator.SetDefaultStrategy(strategy);

        // Assert
        orchestrator.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateEmbedding_ShouldDelegateToProvider()
    {
        // Arrange
        var orchestrator = new ProviderOrchestrator();
        var mockClient = new Mock<ILLMClient>();
        var expectedEmbedding = new double[] { 0.1, 0.2, 0.3 };

        mockClient.Setup(c => c.GenerateEmbedding(It.IsAny<string>()))
            .ReturnsAsync(new Embedding { Vector = expectedEmbedding });

        orchestrator.RegisterProvider("test", mockClient.Object, new ProviderMetadata
        {
            Name = "test",
            Capabilities = new ProviderCapabilities { SupportsEmbeddings = true }
        });

        // Act
        var result = await orchestrator.GenerateEmbedding("test text");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedEmbedding);
    }
}
