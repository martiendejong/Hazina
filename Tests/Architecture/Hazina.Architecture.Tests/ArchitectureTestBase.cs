using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Hazina.Architecture.Tests;

/// <summary>
/// Base class for architecture tests with shared architecture loading
/// </summary>
public abstract class ArchitectureTestBase
{
    // Lazy load the architecture to share across all tests
    private static readonly Lazy<ArchUnitNET.Domain.Architecture> LazyArchitecture = new(() =>
        new ArchLoader()
            .LoadAssemblies(
                // Core layer - LLMs
                typeof(Hazina.LLMs.ILLMClient).Assembly,
                // Core layer - AI Providers
                typeof(Hazina.AI.Providers.Core.ProviderOrchestrator).Assembly,
                // Core layer - FluentAPI
                typeof(Hazina.AI.FluentAPI.Core.Hazina).Assembly
            )
            .Build());

    protected static ArchUnitNET.Domain.Architecture Architecture => LazyArchitecture.Value;
}
