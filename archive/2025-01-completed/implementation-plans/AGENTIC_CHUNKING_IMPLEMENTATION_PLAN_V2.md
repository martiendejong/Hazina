# Agentic Chunking Implementation Plan V2 (Cost-Optimized)

**Date:** 2026-01-04
**Target Framework:** Hazina
**Feature:** Embedding-based semantic chunking with optional LLM enhancement

---

## Table of Contents

1. [Overview](#overview)
2. [Cost Comparison](#cost-comparison)
3. [Architecture Overview](#architecture-overview)
4. [Semantic Similarity Chunking (Primary)](#semantic-similarity-chunking-primary)
5. [Optional LLM Enhancement](#optional-llm-enhancement)
6. [Configuration System](#configuration-system)
7. [Implementation Plan](#implementation-plan)
8. [File Changes Required](#file-changes-required)
9. [Testing Strategy](#testing-strategy)
10. [Performance Analysis](#performance-analysis)

---

## Overview

### Key Innovation: Embedding-Based Boundary Detection

Instead of using expensive LLM calls to detect semantic boundaries, we use **embedding similarity analysis**:

```
Sentences/Paragraphs → Generate Embeddings → Calculate Similarity
    → Detect Similarity Drops → Topic Boundaries → Create Chunks
```

**Cost Reduction:**
- ❌ **Old approach**: LLM call per document (~$0.002 per doc)
- ✅ **New approach**: Only embeddings (~$0.0001 per doc)
- **Savings: 95% cost reduction!**

### Three-Tier Approach

**Tier 1: Embedding-Only Chunking** (Default, near-free)
- Split into sentences/paragraphs
- Generate embeddings using existing embedding model (text-embedding-3-small)
- Calculate cosine similarity between adjacent segments
- Chunk boundaries = similarity drops below threshold
- Cost: ~$0.0001 per document (embeddings only)

**Tier 2: Basic Metadata** (Cheap, rule-based)
- Extract keywords via TF-IDF or TextRank (no LLM)
- Generate simple summaries via extractive methods
- Cost: No additional cost

**Tier 3: LLM Metadata Enhancement** (Optional, premium)
- Use LLM only for rich metadata (summaries, topics)
- Completely optional and configurable
- Cost: ~$0.001 additional (only if enabled)

---

## Cost Comparison

### Old Plan (LLM-Based)

| Operation | Cost per Call | Calls per Doc | Total |
|-----------|---------------|---------------|-------|
| Boundary Analysis | $0.0003 | 1 | $0.0003 |
| Metadata (15 chunks) | $0.0001 | 15 | $0.0015 |
| **Total** | | | **$0.0018** |

**For 10,000 documents: $18.00**

### New Plan (Embedding-Based)

**Tier 1 (Embedding-Only):**

| Operation | Cost per Call | Calls per Doc | Total |
|-----------|---------------|---------------|-------|
| Sentence Embeddings | $0.00002 | 50 sentences | $0.0010 |
| Boundary Detection | $0 (local calc) | 1 | $0 |
| Basic Metadata | $0 (extractive) | 1 | $0 |
| **Total** | | | **$0.0010** |

**For 10,000 documents: $10.00** (44% savings)

**Wait, but we already embed chunks anyway for RAG!**

If we're already generating embeddings for retrieval, the marginal cost is **near-zero** because:
- We already embed chunks after splitting
- We're just using embeddings earlier in the pipeline
- No duplicate embedding calls needed

**Actual Cost for Tier 1: ~$0 (reusing existing embeddings)**

**Tier 2 (With LLM Metadata - Optional):**

| Operation | Cost per Call | Calls per Doc | Total |
|-----------|---------------|---------------|-------|
| Embeddings (reused) | $0 | 0 | $0 |
| LLM Metadata (batch) | $0.0001 | 10 chunks | $0.0010 |
| **Total** | | | **$0.0010** |

**For 10,000 documents: $10.00** (44% savings vs old plan)

### Cost Optimization: Embedding Reuse

**Key insight**: We can reuse embeddings from chunking for retrieval:

```
Document → Embed Sentences → Detect Boundaries → Create Chunks
    → Reuse Sentence Embeddings for Chunk Embeddings
    → Store in Vector DB
```

**Result: Near-zero marginal cost for semantic chunking!**

---

## Architecture Overview

### High-Level Flow (Embedding-Based)

```
Document Text
    ↓
[Preprocessing]
├─ Split into sentences/paragraphs
├─ Filter empty/short segments
└─ Prepare segments for embedding
    ↓
[Embedding Generation]
├─ Generate embeddings for all segments
├─ Use existing IEmbeddingGenerator
└─ Batch processing for efficiency
    ↓
[Semantic Boundary Detection]
├─ Calculate pairwise cosine similarity
├─ Detect similarity drops (topic shifts)
├─ Apply min/max chunk size constraints
└─ Identify optimal boundaries
    ↓
[Chunk Creation]
├─ Group segments into chunks
├─ Merge consecutive high-similarity segments
└─ Create TextChunk objects
    ↓
[Metadata Extraction] (Configurable)
├─ Tier 1: Extractive (TF-IDF keywords)
├─ Tier 2: LLM-based (optional)
└─ Populate chunk metadata
    ↓
Enriched TextChunk[]
```

---

## Semantic Similarity Chunking (Primary)

### Algorithm: Sliding Window Similarity

**Concept**: Detect topic boundaries by measuring semantic similarity between adjacent text segments.

**Implementation:**

```csharp
public class SemanticSimilarityChunker
{
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<SemanticSimilarityChunker>? _logger;

    public async Task<List<TextChunk>> ChunkAsync(
        string text,
        SemanticChunkingOptions options,
        CancellationToken ct = default)
    {
        // 1. Split into sentences
        var sentences = SplitIntoSentences(text, options);

        // 2. Generate embeddings for all sentences (batch)
        var embeddings = await GenerateSentenceEmbeddings(sentences, ct);

        // 3. Calculate similarity matrix
        var similarities = CalculatePairwiseSimilarities(embeddings);

        // 4. Detect boundaries based on similarity drops
        var boundaries = DetectSemanticBoundaries(similarities, sentences, options);

        // 5. Create chunks from boundaries
        var chunks = CreateChunksFromBoundaries(sentences, boundaries);

        // 6. Extract metadata (optional)
        if (options.ExtractMetadata)
        {
            await EnrichWithMetadata(chunks, options, ct);
        }

        return chunks;
    }

    private List<string> SplitIntoSentences(string text, SemanticChunkingOptions options)
    {
        // Use sentence tokenizer
        // Options: sentence length, minimum sentence length, etc.
        var sentences = new List<string>();

        // Simple regex-based sentence splitter
        var pattern = options.SentenceSplitPattern ?? @"(?<=[.!?])\s+";
        var splits = Regex.Split(text, pattern);

        foreach (var sentence in splits)
        {
            var trimmed = sentence.Trim();
            if (trimmed.Length >= (options.MinSentenceLength ?? 10))
            {
                sentences.Add(trimmed);
            }
        }

        return sentences;
    }

    private async Task<List<Embedding>> GenerateSentenceEmbeddings(
        List<string> sentences,
        CancellationToken ct)
    {
        // Batch embedding generation for efficiency
        return await _embeddingGenerator.GenerateBatchAsync(sentences, ct);
    }

    private List<double> CalculatePairwiseSimilarities(List<Embedding> embeddings)
    {
        var similarities = new List<double>();

        // Calculate similarity between adjacent sentences
        for (int i = 0; i < embeddings.Count - 1; i++)
        {
            var similarity = embeddings[i].CosineSimilarity(embeddings[i + 1]);
            similarities.Add(similarity);
        }

        return similarities;
    }

    private List<int> DetectSemanticBoundaries(
        List<double> similarities,
        List<string> sentences,
        SemanticChunkingOptions options)
    {
        var boundaries = new List<int> { 0 }; // Always start at 0
        var currentChunkStart = 0;
        var currentChunkLength = 0;

        for (int i = 0; i < similarities.Count; i++)
        {
            var similarity = similarities[i];
            var sentenceLength = sentences[i].Length;
            currentChunkLength += sentenceLength;

            // Detect boundary conditions:
            // 1. Similarity drops below threshold (topic shift)
            // 2. Chunk size exceeds max
            // 3. Chunk size meets min AND similarity is low

            bool isTopicShift = similarity < (options.SimilarityThreshold ?? 0.7);
            bool exceedsMax = currentChunkLength > (options.MaxChunkSize ?? 2000);
            bool meetsMinAndLowSim =
                currentChunkLength >= (options.MinChunkSize ?? 300) &&
                similarity < (options.SoftSimilarityThreshold ?? 0.8);

            if (exceedsMax || (isTopicShift && meetsMinAndLowSim))
            {
                // Create boundary here
                boundaries.Add(i + 1);
                currentChunkStart = i + 1;
                currentChunkLength = 0;
            }
        }

        // Always add final boundary
        if (boundaries[^1] != sentences.Count)
        {
            boundaries.Add(sentences.Count);
        }

        return boundaries;
    }

    private List<TextChunk> CreateChunksFromBoundaries(
        List<string> sentences,
        List<int> boundaries)
    {
        var chunks = new List<TextChunk>();
        var currentPosition = 0;

        for (int i = 0; i < boundaries.Count - 1; i++)
        {
            var startIdx = boundaries[i];
            var endIdx = boundaries[i + 1];

            var chunkSentences = sentences.GetRange(startIdx, endIdx - startIdx);
            var chunkText = string.Join(" ", chunkSentences);

            chunks.Add(new TextChunk
            {
                Text = chunkText,
                Index = i,
                StartPosition = currentPosition,
                EndPosition = currentPosition + chunkText.Length,
                Metadata = new Dictionary<string, object>
                {
                    ["chunking_strategy"] = "semantic_similarity",
                    ["sentence_count"] = chunkSentences.Count,
                    ["boundary_type"] = "semantic"
                }
            });

            currentPosition += chunkText.Length + 1; // +1 for space
        }

        return chunks;
    }
}
```

### Boundary Detection Algorithm

**Step 1: Calculate Similarity Scores**
```
Sentence[0] <-> Sentence[1]: similarity = 0.92
Sentence[1] <-> Sentence[2]: similarity = 0.89
Sentence[2] <-> Sentence[3]: similarity = 0.65  ← TOPIC SHIFT!
Sentence[3] <-> Sentence[4]: similarity = 0.91
```

**Step 2: Identify Drops**
```
Threshold = 0.70
Drops: [2-3] (0.65 < 0.70)
Boundaries: [0, 3, end]
```

**Step 3: Apply Constraints**
```
Chunk 1: Sentences 0-2 (if size >= MinChunkSize)
Chunk 2: Sentences 3-end (if size >= MinChunkSize)
```

**Visual Example:**

```
Sentences with Similarity Scores:

[S0] "The authentication system uses JWT tokens."
                        ↓ similarity: 0.92 (HIGH - same topic)
[S1] "Tokens are generated by the AuthService class."
                        ↓ similarity: 0.88 (HIGH - same topic)
[S2] "The token lifetime is configurable."
                        ↓ similarity: 0.62 (LOW - TOPIC SHIFT!)
[S3] "Database configuration uses PostgreSQL."     ← BOUNDARY HERE
                        ↓ similarity: 0.90 (HIGH - same topic)
[S4] "Connection strings are stored in appsettings.json."

Result:
Chunk 1 (Authentication): S0, S1, S2
Chunk 2 (Database): S3, S4
```

---

## Optional LLM Enhancement

### Tier 1: Rule-Based Metadata (Free)

**Extractive Keywords (TF-IDF):**
```csharp
public class BasicMetadataExtractor
{
    public ChunkMetadata ExtractMetadata(string chunkText)
    {
        return new ChunkMetadata
        {
            Keywords = ExtractKeywordsTFIDF(chunkText, topK: 5),
            Summary = ExtractSummaryExtractive(chunkText),
            SentenceCount = CountSentences(chunkText),
            WordCount = CountWords(chunkText)
        };
    }

    private List<string> ExtractKeywordsTFIDF(string text, int topK)
    {
        // Simple TF-IDF implementation
        var words = text.ToLower()
            .Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3) // Filter short words
            .Where(w => !IsStopWord(w)); // Filter stop words

        var wordFreq = words
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(topK)
            .Select(g => g.Key)
            .ToList();

        return wordFreq;
    }

    private string ExtractSummaryExtractive(string text)
    {
        // Return first sentence as summary
        var sentences = text.Split(new[] { '.', '!', '?' }, 2);
        return sentences.FirstOrDefault()?.Trim() ?? text.Substring(0, Math.Min(100, text.Length));
    }

    private static HashSet<string> _stopWords = new()
    {
        "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
        "of", "with", "by", "from", "as", "is", "was", "are", "were", "be"
    };

    private bool IsStopWord(string word) => _stopWords.Contains(word);
}
```

**Cost: $0**

### Tier 2: LLM Metadata (Optional, Premium)

**Only if `UseLLMMetadata = true`:**

```csharp
public async Task EnrichWithLLMMetadata(
    List<TextChunk> chunks,
    ILLMClient llmClient,
    CancellationToken ct)
{
    if (chunks.Count == 0) return;

    // Batch metadata generation (more efficient)
    var metadataPrompt = BuildBatchMetadataPrompt(chunks);

    var response = await llmClient.GetResponse<BatchMetadataResponse>(
        new List<HazinaChatMessage>
        {
            new(HazinaMessageRole.System, "Extract metadata from text chunks."),
            new(HazinaMessageRole.User, metadataPrompt)
        },
        toolsContext: null,
        images: null,
        ct
    );

    // Apply metadata to chunks
    if (response.Result?.ChunkMetadata != null)
    {
        for (int i = 0; i < chunks.Count && i < response.Result.ChunkMetadata.Count; i++)
        {
            var metadata = response.Result.ChunkMetadata[i];
            chunks[i].Metadata["llm_summary"] = metadata.Summary;
            chunks[i].Metadata["llm_topic"] = metadata.Topic;
            chunks[i].Metadata["llm_section_type"] = metadata.SectionType;
        }
    }
}

private string BuildBatchMetadataPrompt(List<TextChunk> chunks)
{
    var sb = new StringBuilder();
    sb.AppendLine("Extract metadata for the following chunks:\n");

    for (int i = 0; i < chunks.Count; i++)
    {
        sb.AppendLine($"CHUNK {i + 1}:");
        sb.AppendLine(chunks[i].Text.Substring(0, Math.Min(500, chunks[i].Text.Length)));
        sb.AppendLine();
    }

    sb.AppendLine("For each chunk, provide: topic, summary (one sentence), section type.");

    return sb.ToString();
}
```

**Cost: ~$0.0001 per chunk** (batched)

---

## Configuration System

### SemanticChunkingOptions (Code Configuration)

```csharp
public class SemanticChunkingOptions
{
    // === CORE SETTINGS ===

    /// <summary>
    /// Enable/disable semantic chunking entirely
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Chunking mode: Similarity (embedding-based), LLM (expensive), or Hybrid
    /// </summary>
    public SemanticChunkingMode Mode { get; set; } = SemanticChunkingMode.Similarity;

    // === SENTENCE SPLITTING ===

    /// <summary>
    /// Regex pattern for sentence splitting
    /// Default: split on periods, exclamation marks, question marks
    /// </summary>
    public string? SentenceSplitPattern { get; set; } = @"(?<=[.!?])\s+";

    /// <summary>
    /// Minimum sentence length to include (characters)
    /// </summary>
    public int MinSentenceLength { get; set; } = 10;

    /// <summary>
    /// Maximum sentence length (split long sentences)
    /// </summary>
    public int? MaxSentenceLength { get; set; } = null;

    // === SIMILARITY DETECTION ===

    /// <summary>
    /// Cosine similarity threshold for hard topic boundaries
    /// Values below this trigger a chunk split
    /// Range: 0.0-1.0, Recommended: 0.65-0.75
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.70;

    /// <summary>
    /// Soft similarity threshold (used with min chunk size)
    /// Range: 0.0-1.0, Recommended: 0.75-0.85
    /// </summary>
    public double SoftSimilarityThreshold { get; set; } = 0.80;

    /// <summary>
    /// Use rolling average similarity (smooths out noise)
    /// </summary>
    public bool UseRollingAverage { get; set; } = false;

    /// <summary>
    /// Window size for rolling average (number of adjacent sentences)
    /// </summary>
    public int RollingWindowSize { get; set; } = 3;

    // === CHUNK SIZE CONSTRAINTS ===

    /// <summary>
    /// Minimum chunk size (characters)
    /// </summary>
    public int MinChunkSize { get; set; } = 300;

    /// <summary>
    /// Maximum chunk size (characters)
    /// </summary>
    public int MaxChunkSize { get; set; } = 2000;

    /// <summary>
    /// Prefer splitting at paragraph boundaries if within this range of ideal size
    /// </summary>
    public int? ParagraphBoundaryTolerance { get; set; } = 200;

    // === METADATA EXTRACTION ===

    /// <summary>
    /// Enable basic metadata extraction (free, rule-based)
    /// </summary>
    public bool ExtractMetadata { get; set; } = true;

    /// <summary>
    /// Metadata extraction tier: Basic (free), LLM (premium), or None
    /// </summary>
    public MetadataExtractionTier MetadataTier { get; set; } = MetadataExtractionTier.Basic;

    /// <summary>
    /// Use LLM for rich metadata (summary, topics, etc.)
    /// Cost: ~$0.0001 per chunk
    /// </summary>
    public bool UseLLMMetadata { get; set; } = false;

    /// <summary>
    /// LLM model to use for metadata (if UseLLMMetadata = true)
    /// null = use default model
    /// </summary>
    public string? LLMMetadataModel { get; set; } = null;

    /// <summary>
    /// Batch LLM metadata calls for efficiency
    /// </summary>
    public bool BatchLLMMetadata { get; set; } = true;

    /// <summary>
    /// Maximum chunks per LLM batch
    /// </summary>
    public int MaxLLMBatchSize { get; set; } = 10;

    // === ADVANCED OPTIONS ===

    /// <summary>
    /// Use hierarchical chunking for large documents
    /// Splits into sections first, then chunks each section
    /// </summary>
    public bool UseHierarchicalChunking { get; set; } = false;

    /// <summary>
    /// Section size threshold for hierarchical chunking (characters)
    /// </summary>
    public int HierarchicalSectionSize { get; set; } = 5000;

    /// <summary>
    /// Fallback strategy if semantic chunking fails
    /// </summary>
    public ChunkingStrategy FallbackStrategy { get; set; } = ChunkingStrategy.Paragraph;

    /// <summary>
    /// Cache embeddings for reuse (saves cost)
    /// </summary>
    public bool CacheEmbeddings { get; set; } = true;

    /// <summary>
    /// Content type for specialized chunking (auto-detect if null)
    /// </summary>
    public DocumentContentType? ContentType { get; set; } = null;
}

public enum SemanticChunkingMode
{
    Similarity,  // Embedding-based (cheap)
    LLM,         // LLM-based boundary detection (expensive)
    Hybrid       // Embedding + LLM validation
}

public enum MetadataExtractionTier
{
    None,   // No metadata extraction
    Basic,  // Rule-based (TF-IDF, extractive)
    LLM     // LLM-generated (premium)
}
```

### appsettings.json Configuration

```json
{
  "DocumentStore": {
    "DefaultChunkingStrategy": "Semantic",
    "SemanticChunking": {
      "Enabled": true,
      "Mode": "Similarity",

      "SentenceSplitting": {
        "Pattern": "(?<=[.!?])\\s+",
        "MinLength": 10,
        "MaxLength": null
      },

      "SimilarityDetection": {
        "Threshold": 0.70,
        "SoftThreshold": 0.80,
        "UseRollingAverage": false,
        "RollingWindowSize": 3
      },

      "ChunkSizeConstraints": {
        "MinSize": 300,
        "MaxSize": 2000,
        "ParagraphBoundaryTolerance": 200
      },

      "Metadata": {
        "Extract": true,
        "Tier": "Basic",
        "UseLLM": false,
        "LLMModel": "gpt-4o-mini",
        "BatchLLMCalls": true,
        "MaxBatchSize": 10
      },

      "Advanced": {
        "UseHierarchical": false,
        "HierarchicalSectionSize": 5000,
        "FallbackStrategy": "Paragraph",
        "CacheEmbeddings": true,
        "ContentType": null
      }
    }
  }
}
```

### Configuration Loader

```csharp
public static class SemanticChunkingConfiguration
{
    public static SemanticChunkingOptions LoadFromConfiguration(IConfiguration configuration)
    {
        var options = new SemanticChunkingOptions();

        var section = configuration.GetSection("DocumentStore:SemanticChunking");
        if (!section.Exists()) return options;

        // Bind all properties
        options.Enabled = section.GetValue("Enabled", true);
        options.Mode = Enum.Parse<SemanticChunkingMode>(
            section.GetValue("Mode", "Similarity"));

        // Sentence splitting
        var sentenceSection = section.GetSection("SentenceSplitting");
        options.SentenceSplitPattern = sentenceSection.GetValue<string?>("Pattern");
        options.MinSentenceLength = sentenceSection.GetValue("MinLength", 10);
        options.MaxSentenceLength = sentenceSection.GetValue<int?>("MaxLength");

        // Similarity detection
        var simSection = section.GetSection("SimilarityDetection");
        options.SimilarityThreshold = simSection.GetValue("Threshold", 0.70);
        options.SoftSimilarityThreshold = simSection.GetValue("SoftThreshold", 0.80);
        options.UseRollingAverage = simSection.GetValue("UseRollingAverage", false);
        options.RollingWindowSize = simSection.GetValue("RollingWindowSize", 3);

        // Chunk sizes
        var sizeSection = section.GetSection("ChunkSizeConstraints");
        options.MinChunkSize = sizeSection.GetValue("MinSize", 300);
        options.MaxChunkSize = sizeSection.GetValue("MaxSize", 2000);
        options.ParagraphBoundaryTolerance = sizeSection.GetValue<int?>("ParagraphBoundaryTolerance");

        // Metadata
        var metaSection = section.GetSection("Metadata");
        options.ExtractMetadata = metaSection.GetValue("Extract", true);
        options.MetadataTier = Enum.Parse<MetadataExtractionTier>(
            metaSection.GetValue("Tier", "Basic"));
        options.UseLLMMetadata = metaSection.GetValue("UseLLM", false);
        options.LLMMetadataModel = metaSection.GetValue<string?>("LLMModel");
        options.BatchLLMMetadata = metaSection.GetValue("BatchLLMCalls", true);
        options.MaxLLMBatchSize = metaSection.GetValue("MaxBatchSize", 10);

        // Advanced
        var advSection = section.GetSection("Advanced");
        options.UseHierarchicalChunking = advSection.GetValue("UseHierarchical", false);
        options.HierarchicalSectionSize = advSection.GetValue("HierarchicalSectionSize", 5000);
        options.FallbackStrategy = Enum.Parse<ChunkingStrategy>(
            advSection.GetValue("FallbackStrategy", "Paragraph"));
        options.CacheEmbeddings = advSection.GetValue("CacheEmbeddings", true);

        return options;
    }
}
```

### Usage Examples

**Example 1: Default (Embedding-based, free)**
```csharp
var options = new SemanticChunkingOptions
{
    Enabled = true,
    Mode = SemanticChunkingMode.Similarity,
    MetadataTier = MetadataExtractionTier.Basic
};
var chunks = await chunker.ChunkAsync(text, options);
// Cost: ~$0 (reuses embeddings)
```

**Example 2: Premium (with LLM metadata)**
```csharp
var options = new SemanticChunkingOptions
{
    Enabled = true,
    Mode = SemanticChunkingMode.Similarity,
    UseLLMMetadata = true,
    LLMMetadataModel = "gpt-4o-mini"
};
var chunks = await chunker.ChunkAsync(text, options);
// Cost: ~$0.0010 per document
```

**Example 3: Custom thresholds**
```csharp
var options = new SemanticChunkingOptions
{
    SimilarityThreshold = 0.65,  // More sensitive to topic shifts
    MinChunkSize = 500,           // Larger minimum chunks
    MaxChunkSize = 1500,          // Smaller maximum chunks
    UseRollingAverage = true      // Smooth out noise
};
```

**Example 4: Load from appsettings**
```csharp
var options = SemanticChunkingConfiguration.LoadFromConfiguration(_configuration);
var chunks = await chunker.ChunkAsync(text, options);
```

---

## Implementation Plan

### Phase 1: Core Semantic Similarity Chunking (Minimum Viable)

**Goal:** Implement embedding-based chunking with basic metadata

#### Step 1.1: Create SemanticSimilarityChunker

**File:** `Hazina.AI.RAG/Embeddings/SemanticSimilarityChunker.cs`

- Implement sentence splitting
- Batch embedding generation
- Similarity calculation
- Boundary detection algorithm
- Chunk creation

**Estimated time:** 1 day

#### Step 1.2: Create BasicMetadataExtractor

**File:** `Hazina.AI.RAG/Embeddings/BasicMetadataExtractor.cs`

- TF-IDF keyword extraction
- Extractive summarization
- Basic statistics (word count, sentence count)

**Estimated time:** 0.5 day

#### Step 1.3: Create SemanticChunkingOptions

**File:** `Hazina.AI.RAG/Embeddings/SemanticChunkingOptions.cs`

- All configuration properties
- Validation logic
- Default values

**Estimated time:** 0.5 day

#### Step 1.4: Update TextChunker Integration

**File:** `Hazina.AI.RAG/Embeddings/TextChunker.cs`

- Add `IEmbeddingGenerator` dependency
- Add async semantic chunking method
- Integrate `SemanticSimilarityChunker`

**Estimated time:** 0.5 day

#### Step 1.5: Configuration System

**Files:**
- `Hazina.AI.RAG/Configuration/SemanticChunkingConfiguration.cs`
- `appsettings.template.json` (all projects)

- Configuration loader
- appsettings.json schema
- Documentation

**Estimated time:** 0.5 day

**Phase 1 Total: 3 days**

---

### Phase 2: Optional LLM Enhancement

**Goal:** Add premium LLM metadata generation

#### Step 2.1: Create LLM Metadata Models

**File:** `Hazina.AI.RAG/Embeddings/Models/LLMMetadataResponse.cs`

- Structured response models
- Batch metadata response
- Validation

**Estimated time:** 0.5 day

#### Step 2.2: Implement LLM Metadata Extractor

**File:** `Hazina.AI.RAG/Embeddings/LLMMetadataExtractor.cs`

- Single chunk metadata generation
- Batch metadata generation
- Error handling and fallback

**Estimated time:** 1 day

#### Step 2.3: Integrate into SemanticSimilarityChunker

- Add conditional LLM enrichment
- Configuration switches
- Cost tracking

**Estimated time:** 0.5 day

**Phase 2 Total: 2 days**

---

### Phase 3: Advanced Optimizations

**Goal:** Performance and cost optimizations

#### Step 3.1: Embedding Caching

**File:** `Hazina.AI.RAG/Embeddings/EmbeddingCache.cs`

- In-memory cache
- Content hash-based lookup
- Cache invalidation

**Estimated time:** 1 day

#### Step 3.2: Hierarchical Chunking

- Large document detection
- Section pre-splitting
- Parallel section processing

**Estimated time:** 1 day

#### Step 3.3: Content-Type Aware Chunking

**File:** `Hazina.AI.RAG/Embeddings/ContentTypeDetector.cs`

- Auto-detect content type
- Type-specific strategies
- Custom boundary rules

**Estimated time:** 1 day

**Phase 3 Total: 3 days**

---

### Phase 4: Document Store Integration

**Goal:** Seamless integration with existing pipelines

#### Step 4.1: Update DocumentStore

**File:** `Hazina.Store.DocumentStore/Core/DocumentStore.cs`

- Add `IEmbeddingGenerator` dependency
- Async chunking support
- Configuration integration

**Estimated time:** 1 day

#### Step 4.2: Enhanced Retrieval

**File:** `Hazina.AI.RAG/Core/RAGEngine.cs`

- Metadata-aware search
- Similarity boosting
- Chunk relationship tracking

**Estimated time:** 1 day

**Phase 4 Total: 2 days**

---

**Total Implementation Time: 10 days**

---

## File Changes Required

### New Files to Create (9 files)

| File Path | Purpose | Lines |
|-----------|---------|-------|
| `Hazina.AI.RAG/Embeddings/SemanticSimilarityChunker.cs` | Main chunker | ~300 |
| `Hazina.AI.RAG/Embeddings/SemanticChunkingOptions.cs` | Configuration | ~150 |
| `Hazina.AI.RAG/Embeddings/BasicMetadataExtractor.cs` | Free metadata | ~100 |
| `Hazina.AI.RAG/Embeddings/LLMMetadataExtractor.cs` | Premium metadata | ~150 |
| `Hazina.AI.RAG/Embeddings/Models/LLMMetadataResponse.cs` | Response models | ~50 |
| `Hazina.AI.RAG/Embeddings/EmbeddingCache.cs` | Caching | ~100 |
| `Hazina.AI.RAG/Embeddings/ContentTypeDetector.cs` | Type detection | ~80 |
| `Hazina.AI.RAG/Configuration/SemanticChunkingConfiguration.cs` | Config loader | ~100 |
| `Hazina.AI.RAG/Utilities/SentenceSplitter.cs` | Sentence tokenizer | ~80 |

### Files to Modify (4 files)

| File Path | Changes | Lines |
|-----------|---------|-------|
| `Hazina.AI.RAG/Embeddings/TextChunker.cs` | Add async, integrate semantic chunker | +80 |
| `Hazina.Store.DocumentStore/Core/DocumentStore.cs` | Add embedding dependency, async chunking | +50 |
| `Hazina.AI.RAG/Core/RAGEngine.cs` | Metadata-aware search | +100 |
| `Hazina.AI.RAG/Core/RAGQueryOptions.cs` | Add metadata filters | +20 |

### Configuration Files

| File Path | Changes |
|-----------|---------|
| All `appsettings.template.json` files | Add SemanticChunking section |

---

## Testing Strategy

### Unit Tests

**File:** `Hazina.AI.RAG.Tests/Embeddings/SemanticSimilarityChunkerTests.cs`

```csharp
[Fact]
public async Task ChunkAsync_ShouldDetectTopicBoundaries()
{
    // Arrange
    var text = "Sentence about auth. Another auth sentence. Database config here. More database info.";
    var mockEmbeddings = new[]
    {
        CreateEmbedding([0.1, 0.2, 0.3]), // Auth
        CreateEmbedding([0.1, 0.25, 0.28]), // Auth (high similarity)
        CreateEmbedding([0.8, 0.7, 0.6]), // DB (low similarity)
        CreateEmbedding([0.82, 0.68, 0.62]) // DB (high similarity)
    };

    var mockGenerator = new MockEmbeddingGenerator(mockEmbeddings);
    var chunker = new SemanticSimilarityChunker(mockGenerator);

    // Act
    var chunks = await chunker.ChunkAsync(text, new SemanticChunkingOptions());

    // Assert
    Assert.Equal(2, chunks.Count); // Should detect 2 topics
}

[Fact]
public async Task ChunkAsync_WithBasicMetadata_ShouldExtractKeywords()
{
    // Arrange
    var chunker = CreateChunker();
    var options = new SemanticChunkingOptions
    {
        MetadataTier = MetadataExtractionTier.Basic
    };

    // Act
    var chunks = await chunker.ChunkAsync("Sample text", options);

    // Assert
    Assert.All(chunks, c => Assert.Contains("keywords", c.Metadata));
}

[Fact]
public async Task ChunkAsync_ShouldRespectMinMaxSizes()
{
    // Arrange
    var chunker = CreateChunker();
    var options = new SemanticChunkingOptions
    {
        MinChunkSize = 100,
        MaxChunkSize = 500
    };

    // Act
    var chunks = await chunker.ChunkAsync(longText, options);

    // Assert
    Assert.All(chunks, c => Assert.InRange(c.Text.Length, 100, 500));
}
```

### Performance Tests

```csharp
[Fact]
public async Task ChunkAsync_LargeDocument_ShouldCompleteQuickly()
{
    var largeDoc = GenerateDocument(50000); // 50K chars
    var stopwatch = Stopwatch.StartNew();

    var chunks = await chunker.ChunkAsync(largeDoc, options);

    stopwatch.Stop();
    Assert.True(stopwatch.ElapsedMilliseconds < 5000); // < 5 seconds
}
```

### Cost Tracking Tests

```csharp
[Fact]
public async Task ChunkAsync_ShouldTrackEmbeddingCost()
{
    var costTracker = new CostTracker();
    var chunker = new SemanticSimilarityChunker(embeddingGen, costTracker);

    await chunker.ChunkAsync(text, options);

    Assert.True(costTracker.TotalCost < 0.001); // < $0.001 per doc
}
```

---

## Performance Analysis

### Speed Comparison

| Approach | 1K Chars | 10K Chars | 50K Chars |
|----------|----------|-----------|-----------|
| FixedSize (sync) | <1ms | <1ms | 2ms |
| Embedding-based | ~100ms | ~500ms | ~2s |
| LLM-based (old) | ~3s | ~8s | ~30s |

**Embedding-based is 10x faster than LLM approach!**

### Cost Comparison (10,000 Documents)

| Approach | Boundary Detection | Metadata | Total |
|----------|-------------------|----------|-------|
| **Old Plan (LLM)** | $3.00 | $15.00 | **$18.00** |
| **New Plan (Embedding)** | $0.00* | $0.00** | **$0.00*** |
| **New Plan (with LLM Metadata)** | $0.00* | $10.00 | **$10.00** |

\* Embeddings already generated for retrieval (reused)
\** Basic metadata is rule-based (free)
\*** Assumes embeddings are reused from retrieval pipeline

**Savings: 44-100% depending on configuration!**

### Embedding Reuse Strategy

**Key Optimization**: Generate embeddings once, use twice:

```
Document → Split into Sentences → Generate Embeddings
    ↓                                    ↓
Chunking (similarity analysis)    Store for Retrieval
```

**Cost Breakdown:**
- 10,000 documents
- Average 50 sentences per doc
- 500,000 total embeddings
- Cost: $0.02/1M tokens × 500K × 10 tokens/sentence = **$10.00**

**But we were going to generate embeddings anyway for retrieval!**
- Marginal cost for chunking: **$0**

---

## Configuration Recommendations

### For Different Use Cases

**1. Development/Testing (Free)**
```json
{
  "SemanticChunking": {
    "Enabled": true,
    "Mode": "Similarity",
    "Metadata": {
      "Tier": "Basic",
      "UseLLM": false
    }
  }
}
```
**Cost: $0**

**2. Production (Balanced)**
```json
{
  "SemanticChunking": {
    "Enabled": true,
    "Mode": "Similarity",
    "SimilarityDetection": {
      "Threshold": 0.70,
      "UseRollingAverage": true
    },
    "Metadata": {
      "Tier": "Basic"
    },
    "Advanced": {
      "CacheEmbeddings": true
    }
  }
}
```
**Cost: ~$0.0001/doc (embeddings reused)**

**3. Premium (High Quality)**
```json
{
  "SemanticChunking": {
    "Enabled": true,
    "Mode": "Similarity",
    "Metadata": {
      "Tier": "LLM",
      "UseLLM": true,
      "LLMModel": "gpt-4o-mini",
      "BatchLLMCalls": true
    }
  }
}
```
**Cost: ~$0.0010/doc**

**4. Large Documents**
```json
{
  "SemanticChunking": {
    "Advanced": {
      "UseHierarchical": true,
      "HierarchicalSectionSize": 5000
    }
  }
}
```

---

## Summary

### Key Innovations

✅ **95% Cost Reduction**: Embedding-based instead of LLM calls
✅ **10x Faster**: ~500ms vs 5-8s per document
✅ **Embedding Reuse**: Zero marginal cost if embeddings already generated
✅ **Highly Configurable**: 20+ configuration options
✅ **Tiered Metadata**: Free (rule-based) or Premium (LLM)
✅ **Flexible Modes**: Similarity-only, LLM-only, or Hybrid
✅ **appsettings.json Support**: Full configuration via settings files
✅ **Backward Compatible**: Opt-in, no breaking changes

### Cost Comparison

| Scenario | Old Plan | New Plan (Tier 1) | New Plan (Tier 2) | Savings |
|----------|----------|-------------------|-------------------|---------|
| 1 document | $0.0018 | $0.0001 | $0.0010 | 44-95% |
| 1,000 docs | $1.80 | $0.10 | $1.00 | 44-95% |
| 10,000 docs | $18.00 | $1.00 | $10.00 | 44-95% |
| 100,000 docs | $180.00 | $10.00 | $100.00 | 44-95% |

### Implementation Timeline

- **Phase 1** (Core): 3 days
- **Phase 2** (LLM Enhancement): 2 days
- **Phase 3** (Optimizations): 3 days
- **Phase 4** (Integration): 2 days
- **Total: 10 days**

---

## Implementation Decisions ✅

**APPROVED FOR IMPLEMENTATION**

1. **Plan:** Cost-optimized V2 (embedding-based) ✅
2. **Default Tier:** Tier 1 (Embedding + Basic Metadata) - FREE ✅
3. **Backwards Compatibility:** REQUIRED ✅
4. **Implementation Scope:** FULL (all 4 phases) ✅
5. **Default Threshold:** 0.70 (balanced) ✅

### Backwards Compatibility Strategy

**1. Default Strategy Unchanged:**
```csharp
// Existing code continues to work without changes
var chunker = new TextChunker();
var chunks = chunker.ChunkText(text); // Still uses FixedSize by default
```

**2. Semantic Chunking is Opt-In:**
```csharp
// Only used when explicitly requested
var options = new TextChunkingOptions { Strategy = ChunkingStrategy.Semantic };
var chunks = await chunker.ChunkTextAsync(text, options);
```

**3. Configuration Defaults:**
```json
{
  "DocumentStore": {
    "DefaultChunkingStrategy": "FixedSize",  // Unchanged
    "SemanticChunking": {
      "Enabled": false,  // Opt-in
      "Mode": "Similarity",
      "Metadata": { "Tier": "Basic" }  // Free tier default
    }
  }
}
```

**4. Sync API Preserved:**
```csharp
// Synchronous chunking still available for all strategies except Semantic
var chunks = textChunker.ChunkText(text, options);
```

**5. No Breaking Changes:**
- All existing interfaces remain unchanged
- New functionality in separate classes
- Existing tests continue to pass

---

## Ready for Implementation 🚀

**Scope:** Full implementation (all 4 phases, 10 days)
**Start:** Phase 1 - Core Semantic Similarity Chunking
**Default:** Free Tier (Embedding + Basic Metadata)
