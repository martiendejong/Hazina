using Xunit;
using FluentAssertions;
using Hazina.App.HazinaCoder.Core.Configuration;

namespace Hazina.App.HazinaCoder.Tests.Core.Configuration;

public class FeatureFlagsTests
{
    [Fact]
    public void DefaultFeatureFlags_ShouldHaveCoreFeatures_Enabled()
    {
        // Arrange & Act
        var flags = new FeatureFlags();

        // Assert
        flags.TokenStreaming.Should().BeTrue();
        flags.EventBus.Should().BeTrue();
        flags.MultiAgentCoordination.Should().BeTrue();
        flags.ConsciousnessRecursion.Should().BeTrue();
    }

    [Fact]
    public void DefaultFeatureFlags_ShouldHaveStubFeatures_Disabled()
    {
        // Arrange & Act
        var flags = new FeatureFlags();

        // Assert
        flags.EmotionalProcessing.Should().BeFalse();
        flags.MetaReasoning.Should().BeFalse();
        flags.TimeTravelDebugging.Should().BeFalse();
        flags.QuantumPrediction.Should().BeFalse();
    }

    [Fact]
    public void GetSummary_ShouldReturn_EnabledFeaturesList()
    {
        // Arrange
        var flags = new FeatureFlags
        {
            TokenStreaming = true,
            VisionAnalysis = true,
            MultiAgentCoordination = false
        };

        // Act
        var summary = flags.GetSummary();

        // Assert
        summary.Should().Contain("Streaming");
        summary.Should().Contain("Vision");
        summary.Should().NotContain("Multi-Agent");
    }

    [Fact]
    public void Validate_WithHighRecursionDepth_ShouldWarn()
    {
        // Arrange
        var flags = new FeatureFlags
        {
            ConsciousnessRecursionDepth = 15
        };

        // Act
        var warnings = flags.Validate();

        // Assert
        warnings.Should().Contain(w => w.Contains("stack overflow"));
    }

    [Fact]
    public void Validate_WithVisionButNoProviders_ShouldWarn()
    {
        // Arrange
        var flags = new FeatureFlags
        {
            VisionAnalysis = true,
            OpenAIVision = false,
            ClaudeVision = false
        };

        // Act
        var warnings = flags.Validate();

        // Assert
        warnings.Should().Contain(w => w.Contains("no vision providers"));
    }

    [Fact]
    public void Validate_WithMultiAgentButNoEventBus_ShouldWarn()
    {
        // Arrange
        var flags = new FeatureFlags
        {
            MultiAgentCoordination = true,
            EventBus = false
        };

        // Act
        var warnings = flags.Validate();

        // Assert
        warnings.Should().Contain(w => w.Contains("requires EventBus"));
    }

    [Fact]
    public void LoadFromJson_WithValidJson_ShouldDeserialize()
    {
        // Arrange
        var json = @"{
            ""TokenStreaming"": false,
            ""ConsciousnessRecursionDepth"": 3
        }";

        // Act
        var flags = FeatureFlags.LoadFromJson(json);

        // Assert
        flags.TokenStreaming.Should().BeFalse();
        flags.ConsciousnessRecursionDepth.Should().Be(3);
    }

    [Fact]
    public void LoadFromJson_WithInvalidJson_ShouldReturnDefaults()
    {
        // Arrange
        var json = "{ invalid json }";

        // Act
        var flags = FeatureFlags.LoadFromJson(json);

        // Assert
        flags.Should().NotBeNull();
        flags.TokenStreaming.Should().BeTrue(); // Default value
    }

    [Fact]
    public void FeatureFlagManager_IsEnabled_ShouldReturnCorrectValue()
    {
        // Arrange
        var flags = new FeatureFlags { TokenStreaming = true };
        var manager = new FeatureFlagManager(flags);

        // Act
        var isEnabled = manager.IsEnabled("TokenStreaming");

        // Assert
        isEnabled.Should().BeTrue();
    }

    [Fact]
    public void FeatureFlagManager_Enable_ShouldEnableFeature()
    {
        // Arrange
        var flags = new FeatureFlags { EmotionalProcessing = false };
        var manager = new FeatureFlagManager(flags);

        // Act
        manager.Enable("EmotionalProcessing");

        // Assert
        manager.IsEnabled("EmotionalProcessing").Should().BeTrue();
    }

    [Fact]
    public void FeatureFlagManager_Disable_ShouldDisableFeature()
    {
        // Arrange
        var flags = new FeatureFlags { TokenStreaming = true };
        var manager = new FeatureFlagManager(flags);

        // Act
        manager.Disable("TokenStreaming");

        // Assert
        manager.IsEnabled("TokenStreaming").Should().BeFalse();
    }

    [Fact]
    public void FeatureFlagManager_GetStatus_ShouldReturnAllBooleanFlags()
    {
        // Arrange
        var flags = new FeatureFlags();
        var manager = new FeatureFlagManager(flags);

        // Act
        var status = manager.GetStatus();

        // Assert
        status.Should().ContainKey("TokenStreaming");
        status.Should().ContainKey("EventBus");
        status.Should().ContainKey("VisionAnalysis");
        status.Should().NotContainKey("ConsciousnessRecursionDepth"); // Not boolean
    }
}
