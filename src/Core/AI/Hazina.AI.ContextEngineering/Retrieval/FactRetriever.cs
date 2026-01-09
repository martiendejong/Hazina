using Hazina.AI.ContextEngineering.Interfaces;
using Hazina.AI.ContextEngineering.Models;
using Hazina.Store.FactsStore.Interfaces;
using Hazina.Store.FactsStore.Models;

namespace Hazina.AI.ContextEngineering.Retrieval;

/// <summary>
/// Retriever that fetches compact facts from the facts store
/// </summary>
public class FactRetriever : IContextRetriever
{
    private readonly IFactsStore _factsStore;

    /// <inheritdoc />
    public string Name => "facts";

    /// <summary>
    /// Creates a new fact retriever
    /// </summary>
    /// <param name="factsStore">Facts store instance</param>
    public FactRetriever(IFactsStore factsStore)
    {
        _factsStore = factsStore ?? throw new ArgumentNullException(nameof(factsStore));
    }

    /// <inheritdoc />
    public async Task<List<RetrievalResult>> RetrieveAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        var factQuery = new FactQuery
        {
            QueryText = query.QueryText,
            QueryEmbedding = query.QueryEmbedding,
            TopK = query.TopK,
            MinSimilarity = query.MinSimilarity,
            Tags = query.Tags,
            Types = query.Types,
            Metadata = query.MetadataFilters,
            Ids = query.Ids,
            IncludeEmbeddings = false // Don't need embeddings in results
        };

        var facts = await _factsStore.QueryAsync(factQuery, cancellationToken);

        return facts.Select(fact => new RetrievalResult
        {
            Id = fact.Id,
            Content = fact.Content,
            Score = fact.RelevanceScore,
            Source = Name,
            Type = fact.Type,
            Metadata = fact.Metadata.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value),
            Tags = fact.Tags,
            Timestamp = fact.Created
        }).ToList();
    }
}
