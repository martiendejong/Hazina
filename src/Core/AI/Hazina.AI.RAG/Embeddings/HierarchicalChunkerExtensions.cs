using Microsoft.Extensions.Logging;

namespace Hazina.AI.RAG.Embeddings;

/// <summary>
/// Extensions for HierarchicalChunker to support chunk set generation with summaries.
/// Provides methods to create ChunkSet objects from hierarchical sections.
/// </summary>
public static class HierarchicalChunkerExtensions
{
    /// <summary>
    /// Chunk document hierarchically and generate chunk sets with summaries.
    /// Returns both the individual chunks and the chunk sets.
    /// </summary>
    /// <param name="chunker">The hierarchical chunker instance.</param>
    /// <param name="text">The text to chunk.</param>
    /// <param name="documentId">The document ID for chunk set creation.</param>
    /// <param name="options">Chunking options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of (chunks, chunkSets).</returns>
    public static async Task<(List<TextChunk> Chunks, List<ChunkSet> ChunkSets)> ChunkWithSetsAsync(
        this HierarchicalChunker chunker,
        string text,
        string documentId,
        SemanticChunkingOptions options,
        CancellationToken ct = default)
    {
        // Get chunks using standard hierarchical chunking
        var chunks = await chunker.ChunkAsync(text, options, ct);

        // If chunk set generation is disabled, return empty list
        if (!options.GenerateChunkSets)
        {
            return (chunks, new List<ChunkSet>());
        }

        // Group chunks by section
        var chunkSets = GenerateChunkSets(chunks, documentId, options);

        return (chunks, chunkSets);
    }

    /// <summary>
    /// Generate chunk sets from hierarchically chunked chunks.
    /// Groups chunks by section and generates summaries.
    /// </summary>
    private static List<ChunkSet> GenerateChunkSets(
        List<TextChunk> chunks,
        string documentId,
        SemanticChunkingOptions options)
    {
        var chunkSets = new List<ChunkSet>();

        // Group chunks by section_index metadata
        var sectionGroups = chunks
            .Where(c => c.Metadata.ContainsKey("is_hierarchical") && (bool)c.Metadata["is_hierarchical"])
            .GroupBy(c => c.Metadata.ContainsKey("section_index") ? (int)c.Metadata["section_index"] : -1)
            .Where(g => g.Key >= 0)
            .OrderBy(g => g.Key);

        foreach (var group in sectionGroups)
        {
            var sectionChunks = group.OrderBy(c => c.Index).ToList();
            if (sectionChunks.Count == 0)
                continue;

            var sectionIndex = group.Key;
            var sectionTitle = sectionChunks[0].Metadata.ContainsKey("section_title")
                ? sectionChunks[0].Metadata["section_title"]?.ToString()
                : null;

            // Collect chunk IDs (we'll need to generate proper IDs when chunks are stored)
            var chunkIds = sectionChunks.Select(c => $"{documentId}:chunk:{c.Index}").ToList();

            // Generate section summary
            var sectionSummary = GenerateSectionSummary(sectionChunks, options);

            // Create chunk set
            var chunkSet = ChunkSet.Create(
                documentId: documentId,
                sectionIndex: sectionIndex,
                chunkIds: chunkIds,
                summary: sectionSummary,
                startChunkIndex: sectionChunks.First().Index,
                endChunkIndex: sectionChunks.Last().Index,
                sectionTitle: sectionTitle
            );

            chunkSets.Add(chunkSet);
        }

        return chunkSets;
    }

    /// <summary>
    /// Generate a summary for a section's chunks.
    /// Uses Basic tier (extractive) or LLM tier if configured.
    /// </summary>
    private static string GenerateSectionSummary(
        List<TextChunk> sectionChunks,
        SemanticChunkingOptions options)
    {
        // Combine all chunk texts for the section
        var sectionText = string.Join("\n\n", sectionChunks.Select(c => c.Text));

        // Use configured summary tier
        switch (options.ChunkSetSummaryTier)
        {
            case MetadataExtractionTier.Basic:
            default:
                // Use extractive summarization (free tier)
                var extractor = new BasicMetadataExtractor(options);
                return extractor.ExtractSummary(sectionText);

            case MetadataExtractionTier.LLM:
                // LLM summarization would be implemented here
                // For now, fallback to extractive
                // TODO: Implement LLM-based section summarization
                var fallbackExtractor = new BasicMetadataExtractor(options);
                return fallbackExtractor.ExtractSummary(sectionText);
        }
    }
}
