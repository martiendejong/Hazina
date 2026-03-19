namespace Hazina.LLMs.Capabilities;

/// <summary>
/// Interface for querying provider capabilities at runtime.
/// </summary>
public interface ICapabilityProvider
{
    /// <summary>
    /// Gets all capabilities supported by this provider.
    /// </summary>
    ProviderCapability SupportedCapabilities { get; }

    /// <summary>
    /// Checks if a specific capability is supported.
    /// </summary>
    /// <param name="capability">The capability to check.</param>
    /// <returns>True if the capability is supported, false otherwise.</returns>
    bool SupportsCapability(ProviderCapability capability);

    /// <summary>
    /// Gets all supported capabilities as a list.
    /// </summary>
    /// <returns>List of supported capability names.</returns>
    IEnumerable<string> GetSupportedCapabilityNames();

    /// <summary>
    /// Validates that required capabilities are supported and throws if not.
    /// </summary>
    /// <param name="requiredCapabilities">Capabilities that must be supported.</param>
    /// <exception cref="NotSupportedException">Thrown when required capabilities are missing.</exception>
    void RequireCapabilities(ProviderCapability requiredCapabilities);
}
