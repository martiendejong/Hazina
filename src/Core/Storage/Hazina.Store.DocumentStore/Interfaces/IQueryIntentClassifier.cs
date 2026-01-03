using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Classifies query intent to route to optimal search strategies.
/// </summary>
public interface IQueryIntentClassifier
{
    /// <summary>
    /// Classify the intent of a search query.
    /// </summary>
    /// <param name="query">The user's search query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Classified intent with extracted filters</returns>
    Task<QueryIntent> ClassifyAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Synchronous classification (for simple/fast classifiers).
    /// </summary>
    QueryIntent Classify(string query);
}
