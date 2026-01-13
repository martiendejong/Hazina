# Hazina Cognitive Search - Architecture Design

**Document Version:** 1.0
**Date:** 2026-01-13
**Design By:** Claude Agent (Autonomous Architecture Analysis)
**Repository:** Hazina Framework - C:\Projects\hazina

---

## Executive Summary

This document outlines the architectural design for completing Hazina's cognitive search capabilities. Based on the status analysis, Hazina is **85% complete** with robust backend services. The primary gaps are:

1. **REST API Layer** (expose services via HTTP endpoints)
2. **Key Phrase Extraction** (dedicated NLP module)
3. **Advanced OCR Integration** (beyond LLM vision)
4. **Sentiment Analysis** (document/aspect-level sentiment scoring)

This design provides a comprehensive plan to reach **100% cognitive search capability** while preserving Hazina's architectural strengths: metadata-first design, multi-backend flexibility, and agent-first philosophy.

---

## Architecture Overview

### Current Architecture (Strengths to Preserve)

```
┌─────────────────────────────────────────────────────────────┐
│                    APPLICATION LAYER                        │
│  (Desktop Apps, CLI Tools, Agent Services)                  │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    COGNITIVE SEARCH LAYER                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   RAGEngine  │  │  GraphRAG    │  │Context Engine│      │
│  │  (Standard)  │  │  (Hybrid)    │  │              │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    RETRIEVAL LAYER                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │Semantic      │  │Metadata      │  │Graph         │      │
│  │Retriever     │  │Retriever     │  │Retriever     │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    SCORING & RANKING LAYER                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │Composite     │  │LLM Tag       │  │Reranker      │      │
│  │Scorer        │  │Scorer        │  │              │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    NLP ENRICHMENT LAYER                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │Entity        │  │Relationship  │  │Vision        │      │
│  │Extractor     │  │Extractor     │  │Analyzer      │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    STORAGE LAYER                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │DocumentStore │  │EmbeddingStore│  │GraphStore    │      │
│  │(SQLite)      │  │(pgvector,    │  │(SQLite)      │      │
│  │              │  │ FAISS, etc.) │  │              │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    DATA INGESTION LAYER                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │Text          │  │Binary        │  │External      │      │
│  │Processor     │  │Processor     │  │Connectors    │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
```

### Target Architecture (100% Complete)

Add these layers:

```
┌─────────────────────────────────────────────────────────────┐
│                    API GATEWAY LAYER (NEW)                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │REST API      │  │GraphQL API   │  │WebSocket     │      │
│  │(ASP.NET)     │  │(HotChocolate)│  │(Real-time)   │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│              NLP ENRICHMENT LAYER (ENHANCED)                │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │Entity        │  │Relationship  │  │Vision        │      │
│  │Extractor     │  │Extractor     │  │Analyzer      │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │KeyPhrase (NEW)  │Sentiment (NEW)  │OCR (NEW)     │      │
│  │Extractor     │  │Analyzer      │  │Engine        │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
```

---

## Component Design

### 1. REST API Layer (NEW)

**Purpose:** Expose Hazina's cognitive search capabilities via HTTP endpoints.

**Technology Stack:**
- **Framework:** ASP.NET Core 9.0
- **API Style:** RESTful with OpenAPI 3.1 specification
- **Authentication:** JWT Bearer tokens, API keys
- **Rate Limiting:** AspNetCoreRateLimit
- **Documentation:** Swagger/Swashbuckle

**Module Structure:**
```
Hazina.API.Search/
├── Controllers/
│   ├── SearchController.cs          # Search endpoints
│   ├── DocumentsController.cs       # Document CRUD
│   ├── GraphController.cs           # Knowledge graph queries
│   ├── EmbeddingsController.cs      # Embedding operations
│   └── AnalyticsController.cs       # Search analytics
├── Models/
│   ├── Requests/
│   │   ├── SearchRequest.cs
│   │   ├── DocumentUploadRequest.cs
│   │   └── GraphQueryRequest.cs
│   └── Responses/
│       ├── SearchResponse.cs
│       ├── DocumentResponse.cs
│       └── ErrorResponse.cs
├── Middleware/
│   ├── AuthenticationMiddleware.cs
│   ├── RateLimitingMiddleware.cs
│   ├── ExceptionHandlingMiddleware.cs
│   └── RequestLoggingMiddleware.cs
├── Services/
│   ├── ISearchOrchestrator.cs       # Facade over RAGEngine
│   └── SearchOrchestrator.cs
└── Program.cs
```

**Key Endpoints:**

#### **Search Endpoints**
```http
POST /api/v1/search/query
POST /api/v1/search/semantic
POST /api/v1/search/hybrid
POST /api/v1/search/graph
```

#### **Document Endpoints**
```http
GET    /api/v1/documents
POST   /api/v1/documents/upload
GET    /api/v1/documents/{id}
PUT    /api/v1/documents/{id}
DELETE /api/v1/documents/{id}
GET    /api/v1/documents/{id}/similar
```

#### **Graph Endpoints**
```http
POST   /api/v1/graph/entities/extract
GET    /api/v1/graph/entities/{id}
GET    /api/v1/graph/entities/{id}/relationships
GET    /api/v1/graph/entities/{id}/paths/{targetId}
```

#### **Embedding Endpoints**
```http
POST   /api/v1/embeddings/generate
POST   /api/v1/embeddings/similarity
```

**Request/Response Examples:**

```json
// POST /api/v1/search/query
{
  "query": "What are the security implications of our authentication system?",
  "options": {
    "topK": 10,
    "minSimilarity": 0.7,
    "includeGraphContext": true,
    "includeCitations": true
  }
}

// Response
{
  "answer": "The authentication system has several security considerations...",
  "confidence": 0.92,
  "sources": [
    {
      "documentId": "doc_123",
      "title": "Authentication Security Audit",
      "relevanceScore": 0.95,
      "excerpt": "..."
    }
  ],
  "reasoningPath": [
    "Vector search found 15 relevant documents",
    "Extracted entities: OAuth2, JWT, Session Management",
    "Graph traversal: Security -> Authentication -> OAuth2",
    "Fused 8 documents from vector + 4 from graph"
  ]
}
```

**Authentication & Authorization:**
```csharp
[ApiController]
[Route("api/v1/search")]
[Authorize] // JWT Bearer token required
public class SearchController : ControllerBase
{
    [HttpPost("query")]
    [RateLimit(Name = "search_query", Limit = 100, Period = "1m")]
    public async Task<ActionResult<SearchResponse>> QueryAsync(
        [FromBody] SearchRequest request)
    {
        // Implementation
    }
}
```

**Error Handling:**
```json
{
  "error": {
    "code": "INSUFFICIENT_CONTEXT",
    "message": "Not enough relevant documents found to answer the query.",
    "details": {
      "documentsFound": 2,
      "minimumRequired": 5
    }
  }
}
```

---

### 2. GraphQL API Layer (NEW)

**Purpose:** Provide flexible, client-driven query interface for complex data retrieval.

**Technology Stack:**
- **Framework:** HotChocolate (GraphQL for .NET)
- **Schema-First:** Define schema in GraphQL SDL
- **DataLoader:** Batch and cache database queries
- **Subscriptions:** Real-time updates via WebSocket

**Module Structure:**
```
Hazina.API.GraphQL/
├── Schema/
│   ├── Query.cs                 # Root query type
│   ├── Mutation.cs              # Root mutation type
│   ├── Subscription.cs          # Real-time subscriptions
│   └── Types/
│       ├── DocumentType.cs
│       ├── SearchResultType.cs
│       ├── EntityType.cs
│       └── RelationshipType.cs
├── Resolvers/
│   ├── SearchResolver.cs
│   ├── DocumentResolver.cs
│   └── GraphResolver.cs
└── DataLoaders/
    ├── DocumentByIdDataLoader.cs
    └── EntityByIdDataLoader.cs
```

**Schema Example:**
```graphql
type Query {
  search(
    query: String!
    topK: Int = 10
    minSimilarity: Float = 0.7
  ): SearchResult!

  document(id: ID!): Document

  entities(
    filter: EntityFilter
    limit: Int = 50
  ): [Entity!]!

  entityRelationships(
    entityId: ID!
    relationshipType: String
  ): [Relationship!]!
}

type SearchResult {
  answer: String!
  confidence: Float!
  sources: [Document!]!
  reasoningPath: [String!]!
}

type Document {
  id: ID!
  title: String!
  content: String!
  metadata: Metadata!
  tags: [String!]!
  createdAt: DateTime!
  similar(topK: Int = 5): [Document!]!
}

type Entity {
  id: ID!
  name: String!
  type: EntityType!
  properties: JSON
  documents: [Document!]!
  relationships: [Relationship!]!
}

type Relationship {
  id: ID!
  source: Entity!
  target: Entity!
  type: RelationshipType!
  confidence: Float!
}
```

**Benefits:**
- Single request for complex queries (no over/under-fetching)
- Client-driven data requirements
- Real-time subscriptions for live updates
- Strongly-typed schema with introspection

---

### 3. Key Phrase Extraction Module (NEW)

**Purpose:** Extract important phrases and keywords from documents for improved search and classification.

**Technology Stack:**
- **LLM-based:** Use existing LLM providers for intelligent extraction
- **Statistical:** TF-IDF, RAKE (Rapid Automatic Keyword Extraction)
- **Hybrid:** Combine LLM + statistical for accuracy + speed

**Module Structure:**
```
Hazina.AI.KeyPhraseExtraction/
├── Interfaces/
│   └── IKeyPhraseExtractor.cs
├── Extractors/
│   ├── LLMKeyPhraseExtractor.cs       # LLM-based extraction
│   ├── TfIdfKeyPhraseExtractor.cs     # Statistical TF-IDF
│   ├── RakeKeyPhraseExtractor.cs      # RAKE algorithm
│   └── HybridKeyPhraseExtractor.cs    # Combine strategies
├── Models/
│   ├── KeyPhrase.cs
│   └── KeyPhraseExtractionOptions.cs
└── Scoring/
    └── KeyPhraseScorer.cs
```

**Interface:**
```csharp
public interface IKeyPhraseExtractor
{
    Task<IEnumerable<KeyPhrase>> ExtractAsync(
        string text,
        KeyPhraseExtractionOptions options = null
    );
}

public class KeyPhrase
{
    public string Phrase { get; set; }
    public double Score { get; set; }          // Importance/relevance score
    public int Frequency { get; set; }         // Occurrence count
    public List<int> Positions { get; set; }   // Character positions
}

public class KeyPhraseExtractionOptions
{
    public int MaxPhrases { get; set; } = 20;
    public int MinWordLength { get; set; } = 3;
    public int MaxWordLength { get; set; } = 3;  // Max words per phrase
    public double MinScore { get; set; } = 0.1;
    public ExtractionStrategy Strategy { get; set; } = ExtractionStrategy.Hybrid;
}

public enum ExtractionStrategy
{
    LLM,          // Slow, accurate, context-aware
    TfIdf,        // Fast, statistical
    RAKE,         // Fast, domain-independent
    Hybrid        // LLM for top candidates + statistical for breadth
}
```

**LLM-based Extraction:**
```csharp
public class LLMKeyPhraseExtractor : IKeyPhraseExtractor
{
    private readonly ILLMProvider _llm;

    public async Task<IEnumerable<KeyPhrase>> ExtractAsync(
        string text,
        KeyPhraseExtractionOptions options = null)
    {
        var prompt = $@"Extract the most important key phrases from this text.
Return up to {options.MaxPhrases} phrases as a JSON array of objects with 'phrase' and 'score' (0-1).

Text:
{text}

Output format:
[
  {{ ""phrase"": ""machine learning"", ""score"": 0.95 }},
  {{ ""phrase"": ""neural networks"", ""score"": 0.87 }}
]";

        var response = await _llm.GenerateAsync(prompt);
        return ParseKeyPhrases(response);
    }
}
```

**TF-IDF Extraction:**
```csharp
public class TfIdfKeyPhraseExtractor : IKeyPhraseExtractor
{
    public async Task<IEnumerable<KeyPhrase>> ExtractAsync(
        string text,
        KeyPhraseExtractionOptions options = null)
    {
        // 1. Tokenize and create n-grams (1-3 words)
        var ngrams = ExtractNGrams(text, options.MaxWordLength);

        // 2. Calculate term frequency (TF)
        var termFrequencies = CalculateTermFrequency(ngrams);

        // 3. Load or calculate inverse document frequency (IDF)
        var idf = await LoadIDF();

        // 4. Calculate TF-IDF scores
        var scored = ngrams.Select(ng => new KeyPhrase
        {
            Phrase = ng.Text,
            Score = termFrequencies[ng] * idf[ng],
            Frequency = termFrequencies[ng]
        });

        return scored
            .OrderByDescending(kp => kp.Score)
            .Take(options.MaxPhrases);
    }
}
```

**Hybrid Strategy:**
```csharp
public class HybridKeyPhraseExtractor : IKeyPhraseExtractor
{
    private readonly LLMKeyPhraseExtractor _llmExtractor;
    private readonly TfIdfKeyPhraseExtractor _tfidfExtractor;

    public async Task<IEnumerable<KeyPhrase>> ExtractAsync(
        string text,
        KeyPhraseExtractionOptions options = null)
    {
        // 1. Fast statistical extraction (RAKE/TF-IDF) → get top 50
        var statisticalPhrases = await _tfidfExtractor.ExtractAsync(text,
            new KeyPhraseExtractionOptions { MaxPhrases = 50 });

        // 2. LLM refinement on top candidates → get final 20
        var topCandidates = string.Join(", ",
            statisticalPhrases.Take(30).Select(kp => kp.Phrase));

        var llmPrompt = $@"From these candidate phrases, select the {options.MaxPhrases} most important:
{topCandidates}

Context:
{text.Substring(0, Math.Min(1000, text.Length))}...";

        var llmPhrases = await _llmExtractor.ExtractAsync(llmPrompt, options);

        // 3. Merge scores
        return MergeAndRank(statisticalPhrases, llmPhrases);
    }
}
```

**Integration with Document Processing:**
```csharp
// In DocumentStore or BinaryDocumentProcessor
public async Task ProcessDocumentAsync(Document doc)
{
    // Existing: entity extraction, embeddings, etc.

    // NEW: Key phrase extraction
    var keyPhrases = await _keyPhraseExtractor.ExtractAsync(
        doc.Content,
        new KeyPhraseExtractionOptions { MaxPhrases = 15 }
    );

    // Store as tags or metadata
    doc.Tags.AddRange(keyPhrases.Select(kp => kp.Phrase));
    doc.Metadata["key_phrases"] = JsonSerializer.Serialize(keyPhrases);

    await _documentStore.UpdateAsync(doc);
}
```

**Use Cases:**
- Automatic tagging for documents
- Query expansion (add key phrases to search query)
- Document clustering and topic modeling
- Summarization (extract most important concepts)

---

### 4. Sentiment Analysis Module (NEW)

**Purpose:** Analyze sentiment and emotions in documents for classification, filtering, and insights.

**Technology Stack:**
- **LLM-based:** Use existing LLM providers for nuanced sentiment
- **Lexicon-based:** VADER, TextBlob (fallback for speed)
- **Aspect-based:** Identify sentiment towards specific entities/topics

**Module Structure:**
```
Hazina.AI.SentimentAnalysis/
├── Interfaces/
│   └── ISentimentAnalyzer.cs
├── Analyzers/
│   ├── LLMSentimentAnalyzer.cs        # LLM-based analysis
│   ├── VaderSentimentAnalyzer.cs      # Lexicon-based (VADER)
│   ├── AspectBasedAnalyzer.cs         # Sentiment per entity/topic
│   └── EmotionAnalyzer.cs             # Multi-emotion detection
├── Models/
│   ├── SentimentResult.cs
│   ├── AspectSentiment.cs
│   └── Emotion.cs
└── Scoring/
    └── SentimentScorer.cs
```

**Interface:**
```csharp
public interface ISentimentAnalyzer
{
    Task<SentimentResult> AnalyzeAsync(string text);
    Task<IEnumerable<AspectSentiment>> AnalyzeAspectsAsync(
        string text,
        IEnumerable<string> aspects
    );
}

public class SentimentResult
{
    public SentimentPolarity Polarity { get; set; }  // Positive, Negative, Neutral, Mixed
    public double PositiveScore { get; set; }         // 0-1
    public double NegativeScore { get; set; }         // 0-1
    public double NeutralScore { get; set; }          // 0-1
    public double Confidence { get; set; }            // Overall confidence
    public List<Emotion> Emotions { get; set; }       // Joy, Anger, Sadness, etc.
    public string Summary { get; set; }               // "Strongly positive", "Mildly negative"
}

public enum SentimentPolarity
{
    Positive,
    Negative,
    Neutral,
    Mixed       // Both positive and negative elements
}

public class AspectSentiment
{
    public string Aspect { get; set; }                 // e.g., "authentication", "UI"
    public SentimentPolarity Polarity { get; set; }
    public double Score { get; set; }
    public string Evidence { get; set; }               // Text snippet supporting sentiment
}

public class Emotion
{
    public EmotionType Type { get; set; }
    public double Intensity { get; set; }              // 0-1
}

public enum EmotionType
{
    Joy,
    Sadness,
    Anger,
    Fear,
    Surprise,
    Disgust,
    Trust,
    Anticipation
}
```

**LLM-based Sentiment Analysis:**
```csharp
public class LLMSentimentAnalyzer : ISentimentAnalyzer
{
    private readonly ILLMProvider _llm;

    public async Task<SentimentResult> AnalyzeAsync(string text)
    {
        var prompt = $@"Analyze the sentiment of this text. Return JSON with:
- polarity: ""positive"", ""negative"", ""neutral"", or ""mixed""
- positiveScore: 0-1
- negativeScore: 0-1
- neutralScore: 0-1
- emotions: array of {{ type: ""joy|sadness|anger|fear|surprise"", intensity: 0-1 }}
- summary: brief description

Text:
{text}

Output format:
{{
  ""polarity"": ""positive"",
  ""positiveScore"": 0.85,
  ""negativeScore"": 0.05,
  ""neutralScore"": 0.10,
  ""emotions"": [
    {{ ""type"": ""joy"", ""intensity"": 0.7 }},
    {{ ""type"": ""trust"", ""intensity"": 0.6 }}
  ],
  ""summary"": ""Strongly positive with high trust and moderate joy""
}}";

        var response = await _llm.GenerateAsync(prompt);
        return JsonSerializer.Deserialize<SentimentResult>(response);
    }

    public async Task<IEnumerable<AspectSentiment>> AnalyzeAspectsAsync(
        string text,
        IEnumerable<string> aspects)
    {
        var aspectsList = string.Join(", ", aspects);

        var prompt = $@"Analyze sentiment towards these specific aspects: {aspectsList}

Text:
{text}

For each aspect, return JSON:
[
  {{ ""aspect"": ""authentication"", ""polarity"": ""positive"", ""score"": 0.8, ""evidence"": ""...snippet..."" }}
]";

        var response = await _llm.GenerateAsync(prompt);
        return JsonSerializer.Deserialize<List<AspectSentiment>>(response);
    }
}
```

**Lexicon-based Sentiment (VADER):**
```csharp
public class VaderSentimentAnalyzer : ISentimentAnalyzer
{
    // VADER: Valence Aware Dictionary and sEntiment Reasoner
    // Rule-based, optimized for social media text

    public Task<SentimentResult> AnalyzeAsync(string text)
    {
        // 1. Tokenize text
        var tokens = Tokenize(text);

        // 2. Score each token using VADER lexicon
        var tokenScores = tokens.Select(t => GetLexiconScore(t));

        // 3. Apply grammar rules (negation, intensifiers, etc.)
        var adjustedScores = ApplyGrammarRules(tokens, tokenScores);

        // 4. Calculate compound score
        var compound = CalculateCompoundScore(adjustedScores);

        return Task.FromResult(new SentimentResult
        {
            Polarity = ClassifyPolarity(compound),
            PositiveScore = Math.Max(0, compound),
            NegativeScore = Math.Max(0, -compound),
            NeutralScore = 1 - Math.Abs(compound),
            Confidence = 0.85,  // VADER is generally confident
            Summary = GenerateSummary(compound)
        });
    }
}
```

**Integration with Search:**
```csharp
// Add sentiment as searchable metadata
var sentiment = await _sentimentAnalyzer.AnalyzeAsync(doc.Content);
doc.Metadata["sentiment_polarity"] = sentiment.Polarity.ToString();
doc.Metadata["sentiment_score"] = sentiment.PositiveScore - sentiment.NegativeScore;
doc.Tags.Add($"sentiment_{sentiment.Polarity.ToString().ToLower()}");

// Enable sentiment filtering in search
var filter = new MetadataFilter
{
    CustomMetadata = new Dictionary<string, string>
    {
        ["sentiment_polarity"] = "Positive"
    }
};

var results = await documentStore.SearchTextAsync("customer feedback", filter);
```

**Use Cases:**
- Filter positive/negative customer feedback
- Classify documents by emotional tone
- Monitor brand sentiment in social media
- Alert on negative sentiment spikes
- Aspect-based sentiment for product reviews

---

### 5. Advanced OCR Engine (NEW)

**Purpose:** Extract text from images and PDFs with higher accuracy than LLM vision alone.

**Technology Stack:**
- **Tesseract OCR:** Open-source, multilingual
- **Azure Computer Vision API:** Cloud-based, high accuracy
- **Google Cloud Vision API:** Alternative cloud provider
- **Hybrid:** Tesseract (local, fast) + Cloud (accuracy fallback)

**Module Structure:**
```
Hazina.AI.OCR/
├── Interfaces/
│   └── IOCREngine.cs
├── Engines/
│   ├── TesseractOCREngine.cs          # Open-source Tesseract
│   ├── AzureComputerVisionOCR.cs      # Azure Cognitive Services
│   ├── GoogleVisionOCR.cs             # Google Cloud Vision
│   └── HybridOCREngine.cs             # Fallback chain
├── Models/
│   ├── OCRResult.cs
│   ├── TextBlock.cs
│   └── OCROptions.cs
└── Preprocessing/
    └── ImagePreprocessor.cs            # Enhance image quality
```

**Interface:**
```csharp
public interface IOCREngine
{
    Task<OCRResult> ExtractTextAsync(
        byte[] imageData,
        OCROptions options = null
    );

    Task<OCRResult> ExtractTextFromPdfAsync(
        byte[] pdfData,
        OCROptions options = null
    );
}

public class OCRResult
{
    public string FullText { get; set; }               // All extracted text
    public List<TextBlock> TextBlocks { get; set; }    // Structured blocks
    public double Confidence { get; set; }             // Overall confidence
    public string Language { get; set; }               // Detected language
    public List<string> DetectedLanguages { get; set; }
}

public class TextBlock
{
    public string Text { get; set; }
    public Rectangle BoundingBox { get; set; }         // Position in image
    public double Confidence { get; set; }
    public int PageNumber { get; set; }                // For multi-page PDFs
}

public class OCROptions
{
    public string Language { get; set; } = "eng";      // Language hint
    public bool AutoRotate { get; set; } = true;       // Detect and correct rotation
    public bool PreprocessImage { get; set; } = true;  // Enhance contrast, denoise
    public OCRProvider PreferredProvider { get; set; } = OCRProvider.Hybrid;
}

public enum OCRProvider
{
    Tesseract,
    AzureComputerVision,
    GoogleCloudVision,
    Hybrid        // Try Tesseract first, fallback to cloud if confidence < 0.8
}
```

**Tesseract Implementation:**
```csharp
public class TesseractOCREngine : IOCREngine
{
    public async Task<OCRResult> ExtractTextAsync(
        byte[] imageData,
        OCROptions options = null)
    {
        options ??= new OCROptions();

        // 1. Preprocess image (optional)
        if (options.PreprocessImage)
        {
            imageData = await PreprocessImageAsync(imageData);
        }

        // 2. Run Tesseract
        using var engine = new TesseractEngine(@"./tessdata", options.Language, EngineMode.Default);
        using var img = Pix.LoadFromMemory(imageData);
        using var page = engine.Process(img);

        var fullText = page.GetText();
        var confidence = page.GetMeanConfidence();

        // 3. Extract structured blocks
        var textBlocks = ExtractTextBlocks(page);

        return new OCRResult
        {
            FullText = fullText,
            TextBlocks = textBlocks,
            Confidence = confidence,
            Language = options.Language
        };
    }

    private async Task<byte[]> PreprocessImageAsync(byte[] imageData)
    {
        // Enhance image quality for better OCR
        // - Convert to grayscale
        // - Adjust contrast
        // - Denoise
        // - Binarization (black/white)
        // - Deskew (correct rotation)

        using var image = Image.Load<Rgb24>(imageData);

        image.Mutate(x => x
            .Grayscale()
            .Contrast(1.5f)
            .BinaryThreshold(0.5f)
        );

        using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms);
        return ms.ToArray();
    }
}
```

**Hybrid OCR Strategy:**
```csharp
public class HybridOCREngine : IOCREngine
{
    private readonly TesseractOCREngine _tesseract;
    private readonly AzureComputerVisionOCR _azure;

    public async Task<OCRResult> ExtractTextAsync(
        byte[] imageData,
        OCROptions options = null)
    {
        // 1. Try Tesseract first (fast, local)
        var tesseractResult = await _tesseract.ExtractTextAsync(imageData, options);

        // 2. If confidence is high enough, return
        if (tesseractResult.Confidence >= 0.8)
        {
            return tesseractResult;
        }

        // 3. Otherwise, fallback to Azure (slower, more accurate)
        var azureResult = await _azure.ExtractTextAsync(imageData, options);

        // 4. Return best result
        return azureResult.Confidence > tesseractResult.Confidence
            ? azureResult
            : tesseractResult;
    }
}
```

**Integration with Document Processing:**
```csharp
// In BinaryDocumentProcessor
public async Task<DocumentMetadata> ProcessImageAsync(byte[] imageData, string mimeType)
{
    // Existing: LLM vision analysis
    var visionSummary = await GenerateImageSummaryAsync(imageData);

    // NEW: OCR text extraction
    var ocrResult = await _ocrEngine.ExtractTextAsync(imageData);

    return new DocumentMetadata
    {
        MimeType = mimeType,
        SearchableText = ocrResult.FullText,           // OCR text for search
        Summary = visionSummary,                       // LLM description
        CustomMetadata = new Dictionary<string, string>
        {
            ["ocr_confidence"] = ocrResult.Confidence.ToString(),
            ["ocr_language"] = ocrResult.Language,
            ["has_text"] = (!string.IsNullOrWhiteSpace(ocrResult.FullText)).ToString()
        }
    };
}
```

**PDF Text Extraction:**
```csharp
public async Task<OCRResult> ExtractTextFromPdfAsync(
    byte[] pdfData,
    OCROptions options = null)
{
    var allBlocks = new List<TextBlock>();
    var fullText = new StringBuilder();

    // 1. Try native PDF text extraction first (fast)
    var nativeText = ExtractNativePdfText(pdfData);

    if (!string.IsNullOrWhiteSpace(nativeText))
    {
        // PDF has selectable text, no OCR needed
        return new OCRResult
        {
            FullText = nativeText,
            Confidence = 1.0
        };
    }

    // 2. PDF is scanned image, needs OCR
    // Convert each page to image and run OCR
    using var pdfDoc = PdfDocument.Load(pdfData);

    for (int i = 0; i < pdfDoc.Pages.Count; i++)
    {
        var pageImage = RenderPdfPageToImage(pdfDoc.Pages[i]);
        var pageOCR = await ExtractTextAsync(pageImage, options);

        fullText.AppendLine(pageOCR.FullText);

        foreach (var block in pageOCR.TextBlocks)
        {
            block.PageNumber = i + 1;
            allBlocks.Add(block);
        }
    }

    return new OCRResult
    {
        FullText = fullText.ToString(),
        TextBlocks = allBlocks,
        Confidence = allBlocks.Average(b => b.Confidence)
    };
}
```

**Use Cases:**
- Extract text from scanned documents
- Index images with embedded text
- Process receipts, invoices, forms
- Extract data from screenshots
- Multilingual document processing

---

## Integration Points

### Integration Point 1: API Layer ↔ Backend Services

```csharp
// SearchController uses RAGEngine
public class SearchController : ControllerBase
{
    private readonly RAGEngine _ragEngine;
    private readonly IDocumentStore _documentStore;

    [HttpPost("query")]
    public async Task<ActionResult<SearchResponse>> QueryAsync(
        [FromBody] SearchRequest request)
    {
        var result = await _ragEngine.QueryAsync(
            request.Query,
            new RAGQueryOptions
            {
                TopK = request.TopK,
                MinSimilarity = request.MinSimilarity,
                IncludeGraphContext = request.IncludeGraphContext
            }
        );

        return Ok(MapToSearchResponse(result));
    }
}
```

### Integration Point 2: NLP Modules ↔ Document Processing

```csharp
// Enhanced BinaryDocumentProcessor
public class EnhancedBinaryDocumentProcessor : BinaryDocumentProcessor
{
    private readonly IKeyPhraseExtractor _keyPhraseExtractor;
    private readonly ISentimentAnalyzer _sentimentAnalyzer;
    private readonly IOCREngine _ocrEngine;

    public override async Task<DocumentMetadata> ProcessAsync(
        byte[] content,
        string mimeType)
    {
        var metadata = await base.ProcessAsync(content, mimeType);

        // Add OCR if image
        if (IsImage(mimeType))
        {
            var ocrResult = await _ocrEngine.ExtractTextAsync(content);
            metadata.SearchableText = ocrResult.FullText;
            metadata.CustomMetadata["ocr_confidence"] = ocrResult.Confidence.ToString();
        }

        // Extract key phrases
        var keyPhrases = await _keyPhraseExtractor.ExtractAsync(metadata.SearchableText);
        metadata.Tags.AddRange(keyPhrases.Select(kp => kp.Phrase));

        // Analyze sentiment
        var sentiment = await _sentimentAnalyzer.AnalyzeAsync(metadata.SearchableText);
        metadata.CustomMetadata["sentiment"] = sentiment.Polarity.ToString();
        metadata.CustomMetadata["sentiment_score"] = (sentiment.PositiveScore - sentiment.NegativeScore).ToString();

        return metadata;
    }
}
```

### Integration Point 3: GraphQL ↔ Document Store

```csharp
public class Query
{
    public async Task<SearchResult> SearchAsync(
        string query,
        int topK,
        double minSimilarity,
        [Service] RAGEngine ragEngine)
    {
        var result = await ragEngine.QueryAsync(query, new RAGQueryOptions
        {
            TopK = topK,
            MinSimilarity = minSimilarity
        });

        return new SearchResult
        {
            Answer = result.Answer,
            Confidence = result.Confidence,
            Sources = result.Sources.Select(MapToDocument).ToList(),
            ReasoningPath = result.ReasoningPath
        };
    }
}
```

---

## Data Flow Diagrams

### Flow 1: Document Ingestion with Full Enrichment

```
User Upload (PDF/Image/Text)
          ↓
    API Gateway (POST /api/v1/documents/upload)
          ↓
    Document Type Detection
          ↓
    ┌─────────────────────────────────┐
    │  Content Extraction             │
    │  - Text: Direct extraction      │
    │  - PDF: OCR if needed           │
    │  - Image: OCR + Vision API      │
    └─────────────────────────────────┘
          ↓
    ┌─────────────────────────────────┐
    │  NLP Enrichment (Parallel)      │
    │  ├─ Entity Extraction           │
    │  ├─ Relationship Extraction     │
    │  ├─ Key Phrase Extraction       │
    │  ├─ Sentiment Analysis          │
    │  └─ Embedding Generation        │
    └─────────────────────────────────┘
          ↓
    ┌─────────────────────────────────┐
    │  Storage (Parallel)             │
    │  ├─ DocumentStore (SQLite)      │
    │  ├─ EmbeddingStore (pgvector)   │
    │  └─ GraphStore (entities/rels)  │
    └─────────────────────────────────┘
          ↓
    Response: { documentId, status, metadata }
```

### Flow 2: Hybrid Search Query with GraphRAG

```
User Query: "Who worked on authentication?"
          ↓
    API Gateway (POST /api/v1/search/hybrid)
          ↓
    Query Intent Classification
    (Result: "focused" query about people/relationships)
          ↓
    ┌────────────────────────────────────────────────┐
    │  Parallel Retrieval                            │
    │  ┌──────────────────┐  ┌──────────────────┐   │
    │  │ Vector Search    │  │ Graph Traversal  │   │
    │  │ (semantic sim)   │  │ (WORKED_WITH     │   │
    │  │                  │  │  relationships)  │   │
    │  │ TopK = 20        │  │ Hops = 2         │   │
    │  └──────────────────┘  └──────────────────┘   │
    └────────────────────────────────────────────────┘
          ↓
    ┌─────────────────────────────────┐
    │  Result Fusion                  │
    │  - Weighted Sum (0.6 vector,    │
    │    0.4 graph)                   │
    │  - Deduplication                │
    │  - Reranking (LLM Judge)        │
    └─────────────────────────────────┘
          ↓
    ┌─────────────────────────────────┐
    │  Composite Scoring              │
    │  - Similarity Score             │
    │  - Tag Relevance                │
    │  - Recency                      │
    │  - Position                     │
    └─────────────────────────────────┘
          ↓
    ┌─────────────────────────────────┐
    │  Context Building               │
    │  - Top 10 documents             │
    │  - Token budget: 8000           │
    │  - Citations                    │
    └─────────────────────────────────┘
          ↓
    ┌─────────────────────────────────┐
    │  LLM Answer Generation          │
    │  - Grounded in retrieved docs   │
    │  - Source attribution           │
    │  - Reasoning explanation        │
    └─────────────────────────────────┘
          ↓
    Response: { answer, sources, reasoningPath, confidence }
```

### Flow 3: Real-Time Document Update via WebSocket

```
Document Updated in SQLite
          ↓
    Change Detection (SQLite triggers or polling)
          ↓
    ┌─────────────────────────────────┐
    │  Incremental Processing         │
    │  - Regenerate embeddings        │
    │  - Re-extract entities          │
    │  - Update graph relationships   │
    └─────────────────────────────────┘
          ↓
    GraphQL Subscription Notifier
          ↓
    WebSocket Push to Connected Clients
          ↓
    Client UI Auto-Refresh
```

---

## Suggested Libraries/Services

### Internal (Hazina Packages)
- `Hazina.Store.DocumentStore` - Metadata and full-text search
- `Hazina.Store.EmbeddingStore` - Vector similarity search
- `Hazina.AI.RAG` - RAG and GraphRAG
- `Hazina.LLMs.*` - Multi-provider LLM integration

### External - REST API Layer
- **ASP.NET Core 9.0** - Web framework
- **Swashbuckle** - Swagger/OpenAPI documentation
- **AspNetCoreRateLimit** - Rate limiting
- **Serilog** - Structured logging

### External - GraphQL Layer
- **HotChocolate 13+** - GraphQL server
- **GraphQL.Client** - Client library (for testing)

### External - Key Phrase Extraction
- **SharpNLP** - Natural language processing (TF-IDF, tokenization)
- **RAKE.NET** - RAKE algorithm implementation
- (Use existing `Hazina.LLMs.*` for LLM-based extraction)

### External - Sentiment Analysis
- **VADER.NET** - Lexicon-based sentiment (port of Python VADER)
- **TextBlob.NET** - Simple sentiment API
- (Use existing `Hazina.LLMs.*` for LLM-based sentiment)

### External - OCR
- **Tesseract.NET** (Wrapper: `Tesseract`) - Open-source OCR
- **Azure.AI.Vision** - Azure Computer Vision SDK
- **Google.Cloud.Vision.V1** - Google Cloud Vision SDK
- **ImageSharp** - Image preprocessing

### Cloud Services (Optional)
- **Azure Computer Vision API** - OCR, image analysis
- **Google Cloud Vision API** - Alternative OCR provider
- **AWS Textract** - Document text extraction

---

## Clear Sequence of Implementation Phases

See [ROADMAP.md](./ROADMAP.md) for detailed phased implementation plan.

**High-level phases:**

1. **Phase 1: Foundation** (Weeks 1-2)
   - REST API project setup
   - Core search endpoints
   - Authentication/authorization

2. **Phase 2: NLP Enhancements** (Weeks 3-4)
   - Key phrase extraction
   - Sentiment analysis
   - OCR integration

3. **Phase 3: GraphQL API** (Weeks 5-6)
   - Schema design
   - Resolvers and data loaders
   - Real-time subscriptions

4. **Phase 4: Advanced Features** (Weeks 7-8)
   - Search analytics
   - Real-time indexing
   - Multi-language support

---

## Non-Functional Requirements

### Performance
- **Search Latency:** < 200ms for vector search (p95)
- **API Response Time:** < 500ms for standard queries (p95)
- **Throughput:** 1000 requests/second per instance
- **Concurrent Users:** 10,000+

### Scalability
- **Horizontal Scaling:** Stateless API layer (scale via load balancer)
- **Database:** PostgreSQL with pgvector (proven to billions of vectors)
- **Caching:** Redis for hot queries and embeddings

### Security
- **Authentication:** JWT Bearer tokens, API keys
- **Authorization:** Role-based access control (RBAC)
- **Data Encryption:** TLS 1.3 in transit, AES-256 at rest
- **Rate Limiting:** Per-user, per-endpoint limits

### Observability
- **Logging:** Structured logs (Serilog → Elasticsearch/Seq)
- **Metrics:** Prometheus metrics (request counts, latencies, errors)
- **Tracing:** Distributed tracing (OpenTelemetry)
- **Dashboards:** Grafana dashboards for monitoring

### Reliability
- **Availability:** 99.9% uptime (3 nines)
- **Graceful Degradation:** Continue working when AI services unavailable
- **Backups:** Automated daily backups (SQLite, PostgreSQL)
- **Disaster Recovery:** Point-in-time recovery (PITR)

---

## Architecture Trade-offs

### Decision 1: REST + GraphQL vs. REST Only
**Chosen:** Both REST and GraphQL
**Rationale:**
- REST for simple, cacheable queries (better for CDN caching)
- GraphQL for complex, nested queries (better developer experience)
- Coexist peacefully (different use cases)

**Trade-off:** More API surface area to maintain, but better flexibility

### Decision 2: LLM-based vs. Statistical NLP
**Chosen:** Hybrid approach
**Rationale:**
- LLM-based: High accuracy, context-aware, but slow and costly
- Statistical: Fast, cheap, but less accurate
- Hybrid: Fast statistical first, LLM refinement if needed

**Trade-off:** More complex implementation, but optimal cost/accuracy balance

### Decision 3: Tesseract vs. Cloud OCR
**Chosen:** Hybrid with Tesseract first
**Rationale:**
- Tesseract: Free, local, fast, good for clean images
- Cloud: Expensive, network latency, but higher accuracy
- Hybrid: Try Tesseract first, fallback to cloud if confidence < 0.8

**Trade-off:** More complex, but cost-effective with quality fallback

### Decision 4: Synchronous vs. Asynchronous Document Processing
**Chosen:** Asynchronous (background worker)
**Rationale:**
- Document processing (OCR, embeddings, entity extraction) is slow
- Don't block API response waiting for enrichment
- Return document ID immediately, process in background

**Trade-off:** Eventual consistency (document not immediately searchable), but better UX

### Decision 5: Single Vector Store vs. Multi-Backend
**Chosen:** Keep existing multi-backend architecture
**Rationale:**
- Different use cases need different backends (local SQLite vs. production pgvector)
- Flexibility for users to choose optimal backend
- No vendor lock-in

**Trade-off:** More code to maintain, but architectural flexibility

---

## Conclusion

This architecture design provides a **clear path to 100% cognitive search capability** while:

1. **Preserving Hazina's strengths** (metadata-first, agent-first, multi-backend)
2. **Adding critical missing pieces** (REST API, key phrases, sentiment, OCR)
3. **Maintaining architectural consistency** (same patterns, same quality standards)
4. **Optimizing cost/performance** (hybrid LLM/statistical approaches)

**Next Steps:**
1. Review and approve this design
2. See [ROADMAP.md](./ROADMAP.md) for phased implementation
3. Begin Phase 1: REST API Foundation

**Estimated Effort:** 8 weeks for full implementation (with 2-3 engineers)

**Expected Outcome:** Production-ready cognitive search platform competitive with Azure Cognitive Search, Elasticsearch, and commercial alternatives.

---

**Document Status:** Complete
**Dependencies:** STATUS.md (analysis), ROADMAP.md (implementation plan)
