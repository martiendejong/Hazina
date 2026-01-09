using Hazina.AI.ContextEngineering.Interfaces;
using Hazina.AI.ContextEngineering.Models;
using Hazina.Store.FactsStore.Interfaces;
// DocumentStore types are in global namespace
using Microsoft.Extensions.Logging;

namespace Hazina.AI.ContextEngineering.Retrieval;

/// <summary>
/// Retriever that performs direct ID-based lookup across multiple stores
/// </summary>
public class IdLookupRetriever : IContextRetriever
{
    private readonly IFactsStore? _factsStore;
    private readonly IQueryableMetadataStore? _metadataStore;
    private readonly ILogger<IdLookupRetriever>? _logger;

    /// <inheritdoc />
    public string Name => "lookup";

    /// <summary>
    /// Creates a new ID lookup retriever
    /// </summary>
    /// <param name="factsStore">Optional facts store</param>
    /// <param name="metadataStore">Optional metadata store</param>
    /// <param name="logger">Optional logger</param>
    public IdLookupRetriever(
        IFactsStore? factsStore = null,
        IQueryableMetadataStore? metadataStore = null,
        ILogger<IdLookupRetriever>? logger = null)
    {
        if (factsStore == null && metadataStore == null)
        {
            throw new ArgumentException("At least one store must be provided");
        }

        _factsStore = factsStore;
        _metadataStore = metadataStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<RetrievalResult>> RetrieveAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Ids == null || !query.Ids.Any())
        {
            _logger?.LogWarning("IdLookupRetriever called without IDs");
            return new List<RetrievalResult>();
        }

        var results = new List<RetrievalResult>();

        // Try facts store first
        if (_factsStore != null)
        {
            foreach (var id in query.Ids)
            {
                try
                {
                    var fact = await _factsStore.GetByIdAsync(id, false, cancellationToken);
                    if (fact != null)
                    {
                        results.Add(new RetrievalResult
                        {
                            Id = fact.Id,
                            Content = fact.Content,
                            Score = fact.RelevanceScore,
                            Source = Name,
                            Type = fact.Type,
                            Metadata = fact.Metadata.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value),
                            Tags = fact.Tags,
                            Timestamp = fact.Created
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to retrieve fact with ID: {Id}", id);
                }
            }
        }

        // Try metadata store for IDs not found in facts store
        if (_metadataStore != null)
        {
            var foundIds = results.Select(r => r.Id).ToHashSet();
            var remainingIds = query.Ids.Where(id => !foundIds.Contains(id)).ToList();

            if (remainingIds.Any())
            {
                foreach (var id in remainingIds)
                {
                    try
                    {
                        var meta = await _metadataStore.Get(id);
                        if (meta != null)
                        {
                            results.Add(new RetrievalResult
                            {
                                Id = meta.Id,
                                Content = meta.SearchableText ?? meta.Summary ?? "",
                                Score = 1.0, // Direct lookup = perfect match
                                Source = Name,
                                Type = "document",
                                Metadata = new Dictionary<string, object>
                                {
                                    ["originalPath"] = meta.OriginalPath,
                                    ["mimeType"] = meta.MimeType,
                                    ["customMetadata"] = meta.CustomMetadata ?? new Dictionary<string, string>()
                                },
                                Tags = meta.Tags ?? new List<string>(),
                                Timestamp = meta.Created
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to retrieve document with ID: {Id}", id);
                    }
                }
            }
        }

        _logger?.LogDebug(
            "IdLookupRetriever retrieved {Count} of {Total} requested IDs",
            results.Count,
            query.Ids.Count
        );

        return results;
    }
}
