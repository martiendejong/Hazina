using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Hazina.Architecture.Tests;

/// <summary>
/// Tests to enforce dependency direction: Core <- Tools <- Apps
/// Core layer should NEVER depend on Tools or Apps
/// </summary>
public class DependencyDirectionTests : ArchitectureTestBase
{
    [Fact]
    public void CoreLLMs_ShouldNotDependOn_AIProviders()
    {
        // LLMs.Client should not depend on AI.Providers (providers depend on client, not reverse)
        var rule = Types()
            .That().ResideInAssembly(typeof(Hazina.LLMs.ILLMClient).Assembly)
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(typeof(Hazina.AI.Providers.Core.ProviderOrchestrator).Assembly))
            .Because("ILLMClient is the abstraction that providers implement");

        rule.Check(Architecture);
    }

    [Fact]
    public void CoreLLMs_ShouldNotDependOn_FluentAPI()
    {
        // LLMs.Client should not depend on FluentAPI
        var rule = Types()
            .That().ResideInAssembly(typeof(Hazina.LLMs.ILLMClient).Assembly)
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(typeof(Hazina.AI.FluentAPI.Core.Hazina).Assembly))
            .Because("LLMs is the base abstraction and should not depend on higher layers");

        rule.Check(Architecture);
    }

    [Fact]
    public void AIProviders_DependsOn_CoreLLMs()
    {
        // AI.Providers SHOULD depend on Core LLMs (ILLMClient)
        // This test documents the expected dependency direction
        var providerAssembly = typeof(Hazina.AI.Providers.Core.ProviderOrchestrator).Assembly;
        var llmAssembly = typeof(Hazina.LLMs.ILLMClient).Assembly;

        // Check that the provider assembly references LLMs
        var references = providerAssembly.GetReferencedAssemblies();
        var hasLlmReference = references.Any(r => r.Name == llmAssembly.GetName().Name);

        Assert.True(hasLlmReference, "AI.Providers should reference LLMs (expected dependency direction)");
    }

    [Fact]
    public void FluentAPI_DependsOn_Providers()
    {
        // FluentAPI is allowed to depend on Providers (it's the top-level API)
        var fluentAssembly = typeof(Hazina.AI.FluentAPI.Core.Hazina).Assembly;
        var providerAssembly = typeof(Hazina.AI.Providers.Core.ProviderOrchestrator).Assembly;

        var references = fluentAssembly.GetReferencedAssemblies();
        var hasProviderReference = references.Any(r => r.Name == providerAssembly.GetName().Name);

        Assert.True(hasProviderReference, "FluentAPI should reference Providers (expected dependency direction)");
    }
}
