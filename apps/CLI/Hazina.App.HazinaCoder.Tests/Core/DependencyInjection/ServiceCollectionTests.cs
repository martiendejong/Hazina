using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Hazina.App.HazinaCoder.Core.DependencyInjection;
using Hazina.App.HazinaCoder.Core.Configuration;
using Hazina.App.HazinaCoder.Core.Events;
using Hazina.App.HazinaCoder.Core.Streaming;

namespace Hazina.App.HazinaCoder.Tests.Core.DependencyInjection;

public class ServiceCollectionTests
{
    [Fact]
    public void AddHazinaCoder_ShouldRegister_CoreServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new HazinaCoderConfiguration();

        // Act
        services.AddHazinaCoder(config);
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<AgentEventBus>().Should().NotBeNull();
        provider.GetService<StreamingOrchestrator>().Should().NotBeNull();
        provider.GetService<FeatureFlags>().Should().NotBeNull();
    }

    [Fact]
    public void ConfigureHazinaCoder_ShouldBuild_CompleteServiceProvider()
    {
        // Arrange
        var config = new HazinaCoderConfiguration();

        // Act
        var provider = HazinaCoderStartup.ConfigureHazinaCoder(config);

        // Assert
        provider.Should().NotBeNull();
        provider.GetService<AgentEventBus>().Should().NotBeNull();
    }

    [Fact]
    public void ConfigureMinimal_ShouldBuild_MinimalServices()
    {
        // Arrange
        var config = new HazinaCoderConfiguration();

        // Act
        var provider = HazinaCoderStartup.ConfigureMinimal(config);

        // Assert
        provider.Should().NotBeNull();
        provider.GetService<AgentEventBus>().Should().NotBeNull();
        provider.GetService<StreamingOrchestrator>().Should().NotBeNull();
    }

    [Fact]
    public void HazinaCoderBuilder_ShouldChain_ConfigurationMethods()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new HazinaCoderConfiguration();

        // Act
        var provider = new HazinaCoderBuilder(services, config)
            .AddCoreServices()
            .AddProviders()
            .AddFeatures()
            .AddLogging()
            .Build();

        // Assert
        provider.Should().NotBeNull();
    }
}
