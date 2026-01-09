using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Hazina.Architecture.Tests;

/// <summary>
/// Tests to enforce layer isolation and clean architecture principles
/// </summary>
public class LayerIsolationTests : ArchitectureTestBase
{
    [Fact]
    public void LLMsLayer_ShouldNotDependOn_HigherLayers()
    {
        // Core LLMs should not have circular dependencies with AI layer
        var rule = Types()
            .That().ResideInAssembly(typeof(Hazina.LLMs.ILLMClient).Assembly)
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(typeof(Hazina.AI.Providers.Core.ProviderOrchestrator).Assembly))
            .Because("LLMs is the base abstraction and should not depend on AI layer");

        rule.Check(Architecture);
    }

    [Fact]
    public void ILLMClient_ShouldBeInterface()
    {
        // Verify ILLMClient is properly defined as an interface
        var llmClientType = typeof(Hazina.LLMs.ILLMClient);
        Assert.True(llmClientType.IsInterface, "ILLMClient should be an interface");
    }

    [Fact]
    public void ProviderOrchestrator_ShouldImplementILLMClient()
    {
        // Verify ProviderOrchestrator implements ILLMClient
        var orchestratorType = typeof(Hazina.AI.Providers.Core.ProviderOrchestrator);
        var implementsILLM = typeof(Hazina.LLMs.ILLMClient).IsAssignableFrom(orchestratorType);

        Assert.True(implementsILLM, "ProviderOrchestrator should implement ILLMClient for drop-in compatibility");
    }

    [Fact]
    public void DependencyDirection_ShouldBe_LLMs_Providers_FluentAPI()
    {
        // Document the expected dependency hierarchy
        // LLMs <- Providers <- FluentAPI

        var llmsAssembly = typeof(Hazina.LLMs.ILLMClient).Assembly;
        var providersAssembly = typeof(Hazina.AI.Providers.Core.ProviderOrchestrator).Assembly;
        var fluentAssembly = typeof(Hazina.AI.FluentAPI.Core.Hazina).Assembly;

        // LLMs has no dependencies on Providers or FluentAPI
        var llmsRefs = llmsAssembly.GetReferencedAssemblies();
        Assert.DoesNotContain(llmsRefs, r => r.Name == providersAssembly.GetName().Name);
        Assert.DoesNotContain(llmsRefs, r => r.Name == fluentAssembly.GetName().Name);

        // Providers depends on LLMs
        var providerRefs = providersAssembly.GetReferencedAssemblies();
        Assert.Contains(providerRefs, r => r.Name == llmsAssembly.GetName().Name);

        // FluentAPI depends on Providers
        var fluentRefs = fluentAssembly.GetReferencedAssemblies();
        Assert.Contains(fluentRefs, r => r.Name == providersAssembly.GetName().Name);
    }
}
