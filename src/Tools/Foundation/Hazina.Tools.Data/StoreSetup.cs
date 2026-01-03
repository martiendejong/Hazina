namespace Hazina.Tools.Data
{
    public class StoreSetup
    {
        public ILLMClient LLMClient { get; set; }
        public ITextEmbeddingStore TextEmbeddingStore { get; set; }
        public ITextStore TextStore { get; set; }
        public IDocumentPartStore DocumentPartStore { get; set; }
        public IDocumentStore Store { get; set; }

        /// <summary>
        /// Queryable metadata store for metadata-first search.
        /// Supports filtering by tags, MIME type, date range, and full-text search.
        /// </summary>
        public IQueryableMetadataStore QueryableMetadataStore { get; set; }

        // --- Composite Scoring Components (optional, backwards compatible) ---

        /// <summary>
        /// Tag relevance store for persisting tag scores.
        /// Optional - if null, tag scores are not persisted.
        /// </summary>
        public ITagRelevanceStore TagRelevanceStore { get; set; }

        /// <summary>
        /// Tag scoring service for computing tag relevance.
        /// Optional - if null, uses NoOpTagScoringService (neutral scores).
        /// </summary>
        public ITagScoringService TagScoringService { get; set; }

        /// <summary>
        /// Composite scorer for combining multiple scoring signals.
        /// Optional - if null, uses DefaultCompositeScorer.
        /// </summary>
        public ICompositeScorer CompositeScorer { get; set; }
    }
}

