using Hazina.AI.Providers.Core;
using Hazina.AI.Providers.Health;

namespace Hazina.AI.Providers.Selection;

/// <summary>
/// Selects providers based on various strategies
/// </summary>
public class ProviderSelector : IProviderSelector
{
    private readonly ProviderRegistry _registry;
    private readonly IProviderHealthMonitor _healthMonitor;
    private int _roundRobinIndex = 0;
    private readonly Random _random = new();

    public ProviderSelector(ProviderRegistry registry, IProviderHealthMonitor healthMonitor)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
    }

    /// <summary>
    /// Select a provider based on strategy
    /// </summary>
    public SelectionResult SelectProvider(SelectionStrategy strategy, SelectionContext? context = null)
    {
        context ??= new SelectionContext();

        // Get candidates
        var candidates = GetCandidates(context);

        if (!candidates.Any())
        {
            return new SelectionResult
            {
                Success = false,
                FailureReason = "No providers available that meet the requirements"
            };
        }

        // Apply selection strategy
        var selected = strategy switch
        {
            SelectionStrategy.Priority => SelectByPriority(candidates),
            SelectionStrategy.LeastCost => SelectByLeastCost(candidates),
            SelectionStrategy.FastestResponse => SelectByFastestResponse(candidates),
            SelectionStrategy.RoundRobin => SelectByRoundRobin(candidates),
            SelectionStrategy.Random => SelectByRandom(candidates),
            SelectionStrategy.Specific => SelectSpecific(candidates, context.SpecificProvider),
            SelectionStrategy.Custom => SelectByCustom(candidates, context.CustomSelector),
            _ => SelectByPriority(candidates)
        };

        if (selected == null)
        {
            return new SelectionResult
            {
                Success = false,
                FailureReason = "No provider selected by strategy"
            };
        }

        return new SelectionResult
        {
            Success = true,
            ProviderName = selected.Value.Name,
            Provider = selected.Value.Client,
            Metadata = selected.Value.Metadata,
            HealthStatus = selected.Value.Health
        };
    }

    /// <summary>
    /// Select multiple providers for fallback
    /// </summary>
    public IEnumerable<SelectionResult> SelectProviders(SelectionStrategy strategy, int count, SelectionContext? context = null)
    {
        context ??= new SelectionContext();
        var candidates = GetCandidates(context);

        var selected = strategy switch
        {
            SelectionStrategy.Priority => candidates.Take(count),
            SelectionStrategy.LeastCost => candidates.OrderBy(c => c.Metadata.Pricing.InputCostPer1KTokens).Take(count),
            SelectionStrategy.FastestResponse => candidates.OrderBy(c => c.Health.ResponseTime ?? TimeSpan.MaxValue).Take(count),
            _ => candidates.Take(count)
        };

        return selected.Select(s => new SelectionResult
        {
            Success = true,
            ProviderName = s.Name,
            Provider = s.Client,
            Metadata = s.Metadata,
            HealthStatus = s.Health
        });
    }

    #region Selection Strategies

    private (string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)? SelectByPriority(
        IEnumerable<(string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)> candidates)
    {
        return candidates
            .OrderBy(c => c.Metadata.Priority)
            .FirstOrDefault();
    }

    private (string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)? SelectByLeastCost(
        IEnumerable<(string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)> candidates)
    {
        return candidates
            .OrderBy(c => c.Metadata.Pricing.InputCostPer1KTokens + c.Metadata.Pricing.OutputCostPer1KTokens)
            .FirstOrDefault();
    }

    private (string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)? SelectByFastestResponse(
        IEnumerable<(string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)> candidates)
    {
        return candidates
            .Where(c => c.Health.ResponseTime.HasValue)
            .OrderBy(c => c.Health.ResponseTime!.Value)
            .FirstOrDefault();
    }

    private (string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)? SelectByRoundRobin(
        IEnumerable<(string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)> candidates)
    {
        var list = candidates.ToList();
        if (!list.Any()) return null;

        var index = Interlocked.Increment(ref _roundRobinIndex) % list.Count;
        return list[index];
    }

    private (string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)? SelectByRandom(
        IEnumerable<(string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)> candidates)
    {
        var list = candidates.ToList();
        if (!list.Any()) return null;

        var index = _random.Next(list.Count);
        return list[index];
    }

    private (string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)? SelectSpecific(
        IEnumerable<(string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)> candidates,
        string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return null;

        return candidates.FirstOrDefault(c => c.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));
    }

    private (string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)? SelectByCustom(
        IEnumerable<(string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)> candidates,
        Func<IEnumerable<(string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)>, string?>? customSelector)
    {
        if (customSelector == null)
            return null;

        var selectedName = customSelector(candidates);
        if (string.IsNullOrWhiteSpace(selectedName))
            return null;

        return candidates.FirstOrDefault(c => c.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Candidate Filtering

    private IEnumerable<(string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)> GetCandidates(
        SelectionContext context)
    {
        var providers = _registry.GetEnabledProviders();

        return providers
            .Select(p => (
                p.Name,
                p.Client,
                p.Metadata,
                Health: _healthMonitor.GetHealthStatus(p.Name)
            ))
            .Where(p => !context.ExcludedProviders.Contains(p.Name))
            .Where(p => p.Health.IsHealthy || p.Health.State == HealthState.Unknown) // Exclude unhealthy
            .Where(p => MeetsCapabilityRequirements(p.Metadata, context.RequiredCapabilities))
            .Where(p => MeetsCostRequirements(p.Metadata, context.MaxCostPer1KTokens))
            .Where(p => MeetsResponseTimeRequirements(p.Health, context.MaxResponseTime))
            .Where(p => MeetsSuccessRateRequirements(p.Health, context.MinSuccessRate));
    }

    private bool MeetsCapabilityRequirements(ProviderMetadata metadata, ProviderCapabilities? required)
    {
        if (required == null)
            return true;

        var caps = metadata.Capabilities;

        return (!required.SupportsChat || caps.SupportsChat)
            && (!required.SupportsStreaming || caps.SupportsStreaming)
            && (!required.SupportsEmbeddings || caps.SupportsEmbeddings)
            && (!required.SupportsImages || caps.SupportsImages)
            && (!required.SupportsTTS || caps.SupportsTTS)
            && (!required.SupportsTools || caps.SupportsTools)
            && (!required.SupportsVision || caps.SupportsVision);
    }

    private bool MeetsCostRequirements(ProviderMetadata metadata, decimal? maxCost)
    {
        if (!maxCost.HasValue)
            return true;

        var avgCost = (metadata.Pricing.InputCostPer1KTokens + metadata.Pricing.OutputCostPer1KTokens) / 2m;
        return avgCost <= maxCost.Value;
    }

    private bool MeetsResponseTimeRequirements(ProviderHealthStatus health, TimeSpan? maxResponseTime)
    {
        if (!maxResponseTime.HasValue)
            return true;

        if (!health.ResponseTime.HasValue)
            return true; // Give benefit of doubt if no data

        return health.ResponseTime.Value <= maxResponseTime.Value;
    }

    private bool MeetsSuccessRateRequirements(ProviderHealthStatus health, double? minSuccessRate)
    {
        if (!minSuccessRate.HasValue)
            return true;

        if (health.TotalRequests == 0)
            return true; // Give benefit of doubt if no data

        return health.SuccessRate >= minSuccessRate.Value;
    }

    #endregion
}
