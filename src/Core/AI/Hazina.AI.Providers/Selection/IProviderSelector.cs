namespace Hazina.AI.Providers.Selection;

/// <summary>
/// Interface for provider selection
/// </summary>
public interface IProviderSelector
{
    /// <summary>
    /// Select a provider based on strategy and context
    /// </summary>
    SelectionResult SelectProvider(SelectionStrategy strategy, SelectionContext? context = null);

    /// <summary>
    /// Select multiple providers (for fallback chains)
    /// </summary>
    IEnumerable<SelectionResult> SelectProviders(SelectionStrategy strategy, int count, SelectionContext? context = null);
}
