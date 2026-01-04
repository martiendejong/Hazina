namespace Hazina.AI.RAG.Embeddings;

/// <summary>
/// Configuration options for semantic similarity-based chunking
/// </summary>
public class SemanticChunkingOptions
{
    // === CORE SETTINGS ===

    /// <summary>
    /// Enable/disable semantic chunking entirely
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Chunking mode: Similarity (embedding-based), LLM (expensive), or Hybrid
    /// Default: Similarity (cost-efficient)
    /// </summary>
    public SemanticChunkingMode Mode { get; set; } = SemanticChunkingMode.Similarity;

    // === SENTENCE SPLITTING ===

    /// <summary>
    /// Regex pattern for sentence splitting
    /// Default: split on periods, exclamation marks, question marks followed by whitespace
    /// Example: @"(?<=[.!?])\s+"
    /// </summary>
    public string SentenceSplitPattern { get; set; } = @"(?<=[.!?])\s+";

    /// <summary>
    /// Minimum sentence length to include (characters)
    /// Sentences shorter than this will be merged with adjacent sentences
    /// Default: 10
    /// </summary>
    public int MinSentenceLength { get; set; } = 10;

    /// <summary>
    /// Maximum sentence length (split long sentences if exceeded)
    /// null = no limit
    /// Default: null
    /// </summary>
    public int? MaxSentenceLength { get; set; } = null;

    // === SIMILARITY DETECTION ===

    /// <summary>
    /// Cosine similarity threshold for hard topic boundaries
    /// When similarity between adjacent sentences falls below this, create a chunk boundary
    /// Range: 0.0-1.0
    /// Lower values = more sensitive to topic shifts (more, smaller chunks)
    /// Higher values = less sensitive (fewer, larger chunks)
    /// Recommended: 0.65-0.75
    /// Default: 0.70
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.70;

    /// <summary>
    /// Soft similarity threshold (used with min chunk size constraint)
    /// Only create boundary if chunk size >= MinChunkSize AND similarity below this threshold
    /// Should be higher than SimilarityThreshold
    /// Range: 0.0-1.0
    /// Recommended: 0.75-0.85
    /// Default: 0.80
    /// </summary>
    public double SoftSimilarityThreshold { get; set; } = 0.80;

    /// <summary>
    /// Use rolling average similarity (smooths out noise)
    /// Averages similarity across a window of adjacent sentences
    /// Reduces false positives from single outlier sentences
    /// Default: false
    /// </summary>
    public bool UseRollingAverage { get; set; } = false;

    /// <summary>
    /// Window size for rolling average (number of adjacent sentence pairs)
    /// Only used if UseRollingAverage = true
    /// Default: 3
    /// </summary>
    public int RollingWindowSize { get; set; } = 3;

    // === CHUNK SIZE CONSTRAINTS ===

    /// <summary>
    /// Minimum chunk size (characters)
    /// Prevents creating very small chunks
    /// Default: 300
    /// </summary>
    public int MinChunkSize { get; set; } = 300;

    /// <summary>
    /// Maximum chunk size (characters)
    /// Forces chunk split even if similarity is high
    /// Default: 2000
    /// </summary>
    public int MaxChunkSize { get; set; } = 2000;

    /// <summary>
    /// Prefer splitting at paragraph boundaries if chunk size is within this range of ideal size
    /// null = don't prefer paragraph boundaries
    /// Example: If target chunk is 1000 chars and tolerance is 200,
    ///          split at paragraph if it creates chunks between 800-1200 chars
    /// Default: 200
    /// </summary>
    public int? ParagraphBoundaryTolerance { get; set; } = 200;

    // === METADATA EXTRACTION ===

    /// <summary>
    /// Enable metadata extraction (keywords, summaries, etc.)
    /// Default: true
    /// </summary>
    public bool ExtractMetadata { get; set; } = true;

    /// <summary>
    /// Metadata extraction tier: None, Basic (free), or LLM (premium)
    /// - None: No metadata extraction
    /// - Basic: Rule-based (TF-IDF keywords, extractive summaries) - FREE
    /// - LLM: AI-generated metadata (topics, summaries, section types) - ~$0.0001 per chunk
    /// Default: Basic (free)
    /// </summary>
    public MetadataExtractionTier MetadataTier { get; set; } = MetadataExtractionTier.Basic;

    /// <summary>
    /// Use LLM for rich metadata generation (summary, topics, section types)
    /// Only used if MetadataTier = LLM
    /// Cost: ~$0.0001 per chunk
    /// Default: false
    /// </summary>
    public bool UseLLMMetadata { get; set; } = false;

    /// <summary>
    /// LLM model to use for metadata extraction (if UseLLMMetadata = true)
    /// null = use default model from ILLMClient
    /// Example: "gpt-4o-mini"
    /// Default: null
    /// </summary>
    public string? LLMMetadataModel { get; set; } = null;

    /// <summary>
    /// Batch LLM metadata calls for efficiency
    /// Sends multiple chunks in a single LLM request
    /// Reduces latency and can reduce costs
    /// Default: true
    /// </summary>
    public bool BatchLLMMetadata { get; set; } = true;

    /// <summary>
    /// Maximum chunks to include in a single LLM batch request
    /// Prevents request size from exceeding token limits
    /// Default: 10
    /// </summary>
    public int MaxLLMBatchSize { get; set; } = 10;

    // === ADVANCED OPTIONS ===

    /// <summary>
    /// Use hierarchical chunking for large documents
    /// Splits document into sections first, then chunks each section independently
    /// Improves performance for documents larger than HierarchicalSectionSize
    /// Default: false
    /// </summary>
    public bool UseHierarchicalChunking { get; set; } = false;

    /// <summary>
    /// Document size threshold for hierarchical chunking (characters)
    /// Documents larger than this will be pre-split into sections
    /// Default: 5000
    /// </summary>
    public int HierarchicalSectionSize { get; set; } = 5000;

    /// <summary>
    /// Fallback strategy if semantic chunking fails (e.g., no embeddings available, error)
    /// Default: Paragraph
    /// </summary>
    public ChunkingStrategy FallbackStrategy { get; set; } = ChunkingStrategy.Paragraph;

    /// <summary>
    /// Cache sentence embeddings for reuse within the same document
    /// Reduces embedding API calls (cost savings)
    /// Default: true
    /// </summary>
    public bool CacheEmbeddings { get; set; } = true;

    /// <summary>
    /// Content type for specialized chunking behavior
    /// null = auto-detect
    /// Options: Prose, TechnicalDoc, Code, Markdown, Json, Mixed
    /// Default: null (auto-detect)
    /// </summary>
    public DocumentContentType? ContentType { get; set; } = null;

    /// <summary>
    /// Number of top keywords to extract (Basic metadata tier)
    /// Default: 5
    /// </summary>
    public int TopKeywordsCount { get; set; } = 5;
}

/// <summary>
/// Semantic chunking modes
/// </summary>
public enum SemanticChunkingMode
{
    /// <summary>
    /// Embedding-based similarity detection (cost-efficient, recommended)
    /// Uses cosine similarity between sentence embeddings to detect topic boundaries
    /// Cost: Near-zero if embeddings are reused for retrieval
    /// </summary>
    Similarity,

    /// <summary>
    /// LLM-based boundary detection (expensive, highest quality)
    /// Uses LLM to analyze document structure and propose boundaries
    /// Cost: ~$0.0003 per document analysis
    /// </summary>
    LLM,

    /// <summary>
    /// Hybrid approach: embedding detection + LLM validation
    /// Uses embeddings for initial boundaries, LLM to validate and refine
    /// Cost: ~$0.0002 per document
    /// </summary>
    Hybrid
}

/// <summary>
/// Metadata extraction tiers
/// </summary>
public enum MetadataExtractionTier
{
    /// <summary>
    /// No metadata extraction
    /// </summary>
    None,

    /// <summary>
    /// Rule-based metadata extraction (FREE)
    /// - TF-IDF keywords
    /// - Extractive summaries (first sentence)
    /// - Basic statistics (word count, sentence count)
    /// </summary>
    Basic,

    /// <summary>
    /// LLM-generated metadata (premium)
    /// - AI-generated summaries
    /// - Topic classification
    /// - Section type detection
    /// - Importance scoring
    /// Cost: ~$0.0001 per chunk
    /// </summary>
    LLM
}

/// <summary>
/// Document content types for specialized chunking
/// </summary>
public enum DocumentContentType
{
    /// <summary>
    /// Natural language prose (articles, books, essays)
    /// </summary>
    Prose,

    /// <summary>
    /// Technical documentation (API docs, manuals, guides)
    /// </summary>
    TechnicalDoc,

    /// <summary>
    /// Source code files
    /// </summary>
    Code,

    /// <summary>
    /// Markdown formatted documents
    /// </summary>
    Markdown,

    /// <summary>
    /// JSON or other structured data
    /// </summary>
    Json,

    /// <summary>
    /// Mixed content types
    /// </summary>
    Mixed
}
