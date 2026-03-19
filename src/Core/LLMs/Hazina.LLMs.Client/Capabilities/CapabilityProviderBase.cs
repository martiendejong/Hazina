namespace Hazina.LLMs.Capabilities;

/// <summary>
/// Base implementation of capability provider.
/// </summary>
public abstract class CapabilityProviderBase : ICapabilityProvider
{
    /// <inheritdoc/>
    public abstract ProviderCapability SupportedCapabilities { get; }

    /// <inheritdoc/>
    public bool SupportsCapability(ProviderCapability capability)
    {
        return (SupportedCapabilities & capability) == capability;
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetSupportedCapabilityNames()
    {
        var capabilities = new List<string>();
        var allCapabilities = Enum.GetValues<ProviderCapability>();

        foreach (var capability in allCapabilities)
        {
            if (capability != ProviderCapability.None &&
                capability != ProviderCapability.All &&
                SupportsCapability(capability))
            {
                capabilities.Add(capability.ToString());
            }
        }

        return capabilities;
    }

    /// <inheritdoc/>
    public void RequireCapabilities(ProviderCapability requiredCapabilities)
    {
        if (!SupportsCapability(requiredCapabilities))
        {
            var missing = GetMissingCapabilities(requiredCapabilities);
            throw new NotSupportedException(
                $"Provider does not support required capabilities: {string.Join(", ", missing)}");
        }
    }

    /// <summary>
    /// Gets the list of missing capabilities.
    /// </summary>
    protected IEnumerable<string> GetMissingCapabilities(ProviderCapability requiredCapabilities)
    {
        var missing = new List<string>();
        var allCapabilities = Enum.GetValues<ProviderCapability>();

        foreach (var capability in allCapabilities)
        {
            if (capability != ProviderCapability.None &&
                capability != ProviderCapability.All &&
                (requiredCapabilities & capability) == capability &&
                !SupportsCapability(capability))
            {
                missing.Add(capability.ToString());
            }
        }

        return missing;
    }
}
