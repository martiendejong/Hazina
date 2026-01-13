# Hazina Cognitive Search - Implementation Roadmap

**Document Version:** 1.0
**Date:** 2026-01-13
**Planning By:** Claude Agent (Autonomous Architecture Analysis)
**Repository:** Hazina Framework - C:\Projects\hazina

---

## Executive Summary

This roadmap outlines a **4-phase, 8-week implementation plan** to complete Hazina's cognitive search capabilities. The plan is designed for a team of 2-3 engineers and follows an incremental delivery model with working software at each phase.

**Current State:** 85% complete (backend services production-ready)
**Target State:** 100% complete (full cognitive search platform with REST/GraphQL APIs)

**Key Deliverables:**
- Phase 1: REST API Foundation (Weeks 1-2)
- Phase 2: NLP Enhancements (Weeks 3-4)
- Phase 3: GraphQL API (Weeks 5-6)
- Phase 4: Advanced Features (Weeks 7-8)

---

## Phase Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    PHASE DEPENDENCY CHART                       │
└─────────────────────────────────────────────────────────────────┘

Phase 1: REST API Foundation (Weeks 1-2)
   ├─ Task 1.1: Project setup & infrastructure
   ├─ Task 1.2: Core search endpoints
   ├─ Task 1.3: Document endpoints
   └─ Task 1.4: Authentication & security
         ↓
Phase 2: NLP Enhancements (Weeks 3-4)
   ├─ Task 2.1: Key phrase extraction
   ├─ Task 2.2: Sentiment analysis
   └─ Task 2.3: Advanced OCR integration
         ↓
Phase 3: GraphQL API (Weeks 5-6)
   ├─ Task 3.1: Schema design
   ├─ Task 3.2: Resolvers & data loaders
   └─ Task 3.3: Real-time subscriptions
         ↓
Phase 4: Advanced Features (Weeks 7-8)
   ├─ Task 4.1: Search analytics
   ├─ Task 4.2: Performance optimization
   └─ Task 4.3: Documentation & deployment

Total Duration: 8 weeks (160 work hours with 2 engineers)
```

---

## Phase 1: REST API Foundation (Weeks 1-2)

**Goal:** Build production-ready REST API to expose existing search capabilities via HTTP endpoints.

**Duration:** 2 weeks (40 work hours per engineer)

**Team:** 2 engineers
- Engineer A: API infrastructure, authentication, middleware
- Engineer B: Search/document endpoints, response mapping

---

### Task 1.1: Project Setup & Infrastructure (Days 1-2)

**Subtasks:**
1. Create new project `Hazina.API.Search`
2. Set up ASP.NET Core 9.0 with minimal API or controllers
3. Configure dependency injection
4. Add Swashbuckle for Swagger/OpenAPI
5. Set up Serilog for structured logging
6. Configure CORS policies
7. Add health check endpoints

**Dependencies:** None

**Deliverables:**
- ✅ Running API project with `/health` and `/swagger` endpoints
- ✅ Structured logging to console and file
- ✅ Dependency injection configured

**Code Structure:**
```
Hazina.API.Search/
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── Hazina.API.Search.csproj
└── Extensions/
    ├── ServiceCollectionExtensions.cs
    └── ApplicationBuilderExtensions.cs
```

**Acceptance Criteria:**
- [ ] `dotnet run` starts API on `https://localhost:5001`
- [ ] `GET /health` returns `200 OK` with status
- [ ] `GET /swagger` shows interactive API documentation
- [ ] Logs are written to console and `logs/api-.log`

**Effort:** 16 hours (2 days × 2 engineers)

---

### Task 1.2: Core Search Endpoints (Days 3-4)

**Subtasks:**
1. Create `SearchController`
2. Implement `POST /api/v1/search/query` (natural language)
3. Implement `POST /api/v1/search/semantic` (vector similarity)
4. Implement `POST /api/v1/search/hybrid` (vector + graph)
5. Create request/response DTOs
6. Add input validation
7. Map `RAGEngine` results to API responses
8. Add error handling and logging

**Dependencies:** Task 1.1

**Deliverables:**
- ✅ 3 working search endpoints
- ✅ Request/response models documented in Swagger
- ✅ Error responses follow RFC 7807 Problem Details

**Code:**
```csharp
// SearchController.cs
[ApiController]
[Route("api/v1/search")]
public class SearchController : ControllerBase
{
    private readonly RAGEngine _ragEngine;
    private readonly ILogger<SearchController> _logger;

    [HttpPost("query")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SearchResponse>> QueryAsync(
        [FromBody] SearchRequest request)
    {
        _logger.LogInformation("Search query: {Query}", request.Query);

        var result = await _ragEngine.QueryAsync(
            request.Query,
            new RAGQueryOptions
            {
                TopK = request.TopK ?? 10,
                MinSimilarity = request.MinSimilarity ?? 0.7
            }
        );

        return Ok(MapToSearchResponse(result));
    }

    // Similar for /semantic and /hybrid
}
```

**Request Example:**
```json
POST /api/v1/search/query
{
  "query": "What are the security implications of OAuth2?",
  "topK": 10,
  "minSimilarity": 0.7,
  "includeGraphContext": true,
  "includeCitations": true
}
```

**Response Example:**
```json
{
  "answer": "OAuth2 has several security considerations including...",
  "confidence": 0.92,
  "sources": [
    {
      "documentId": "doc_123",
      "title": "OAuth2 Security Best Practices",
      "relevanceScore": 0.95,
      "excerpt": "OAuth2 requires careful implementation to avoid..."
    }
  ],
  "reasoningPath": [
    "Vector search found 15 relevant documents",
    "Extracted entities: OAuth2, Security, Authentication",
    "Fused 8 vector results with 4 graph results"
  ],
  "metadata": {
    "totalDocumentsSearched": 15000,
    "searchTimeMs": 142
  }
}
```

**Acceptance Criteria:**
- [ ] `POST /api/v1/search/query` returns answer with sources
- [ ] `POST /api/v1/search/semantic` returns ranked documents
- [ ] `POST /api/v1/search/hybrid` combines vector + graph results
- [ ] Invalid requests return `400 Bad Request` with details
- [ ] All endpoints documented in Swagger

**Effort:** 16 hours (2 days × 2 engineers)

---

### Task 1.3: Document Endpoints (Days 5-6)

**Subtasks:**
1. Create `DocumentsController`
2. Implement `GET /api/v1/documents` (list with filtering)
3. Implement `POST /api/v1/documents/upload` (file upload)
4. Implement `GET /api/v1/documents/{id}` (retrieve by ID)
5. Implement `PUT /api/v1/documents/{id}` (update metadata)
6. Implement `DELETE /api/v1/documents/{id}` (soft delete)
7. Implement `GET /api/v1/documents/{id}/similar` (find similar)
8. Add multipart form-data support for uploads
9. Add pagination for list endpoint

**Dependencies:** Task 1.1

**Deliverables:**
- ✅ Full CRUD API for documents
- ✅ File upload with multipart/form-data
- ✅ Pagination (page, pageSize, total)
- ✅ Filtering by tags, MIME type, date range

**Code:**
```csharp
[ApiController]
[Route("api/v1/documents")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentStore _documentStore;

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100_000_000)] // 100 MB
    public async Task<ActionResult<DocumentResponse>> UploadAsync(
        IFormFile file,
        [FromForm] string? tags = null,
        [FromForm] bool generateEmbeddings = true)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        var content = stream.ToArray();

        var document = await _documentStore.StoreFromBytesAsync(
            file.FileName,
            content,
            file.ContentType,
            tags?.Split(',')
        );

        if (generateEmbeddings)
        {
            await _documentStore.GenerateEmbeddingsAsync(document.Id);
        }

        return CreatedAtAction(
            nameof(GetByIdAsync),
            new { id = document.Id },
            MapToDocumentResponse(document)
        );
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<DocumentResponse>>> GetAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? tags = null,
        [FromQuery] string? mimeType = null,
        [FromQuery] DateTime? createdAfter = null)
    {
        var filter = new MetadataFilter
        {
            Tags = tags?.Split(','),
            MimeTypePrefix = mimeType,
            CreatedAfter = createdAfter
        };

        var (documents, total) = await _documentStore.GetPagedAsync(
            page,
            pageSize,
            filter
        );

        return Ok(new PagedResponse<DocumentResponse>
        {
            Items = documents.Select(MapToDocumentResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    // Similar for GET /{id}, PUT /{id}, DELETE /{id}, GET /{id}/similar
}
```

**Acceptance Criteria:**
- [ ] Upload 10 MB PDF, receives `201 Created` with document ID
- [ ] `GET /api/v1/documents?page=1&pageSize=20&tags=research` returns paginated results
- [ ] `GET /api/v1/documents/{id}` returns full document metadata
- [ ] `GET /api/v1/documents/{id}/similar` returns 10 similar documents
- [ ] `DELETE /api/v1/documents/{id}` marks document as deleted

**Effort:** 16 hours (2 days × 2 engineers)

---

### Task 1.4: Authentication & Security (Days 7-10)

**Subtasks:**
1. Implement JWT Bearer token authentication
2. Add API key authentication (for service-to-service)
3. Configure authorization policies
4. Add rate limiting (AspNetCoreRateLimit)
5. Add request logging middleware
6. Add exception handling middleware
7. Configure HTTPS and HSTS
8. Add CORS policies
9. Security headers (X-Frame-Options, X-Content-Type-Options, etc.)
10. Write integration tests

**Dependencies:** Tasks 1.2, 1.3

**Deliverables:**
- ✅ JWT authentication working
- ✅ API key authentication working
- ✅ Rate limiting: 100 requests/minute per user
- ✅ All endpoints require authentication
- ✅ Exception handling returns RFC 7807 Problem Details
- ✅ Integration tests for auth flows

**Code:**
```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ReadAccess", policy => policy.RequireRole("User", "Admin"));
});

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("search_query", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
    });
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
```

**Middleware:**
```csharp
// ExceptionHandlingMiddleware.cs
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = ex.Message,
                Instance = context.Request.Path
            };

            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }
}
```

**Acceptance Criteria:**
- [ ] Request without auth token returns `401 Unauthorized`
- [ ] Request with invalid token returns `401 Unauthorized`
- [ ] Request with valid token succeeds
- [ ] Exceeding rate limit returns `429 Too Many Requests`
- [ ] Unhandled exceptions return `500 Internal Server Error` with Problem Details
- [ ] All responses include security headers

**Effort:** 32 hours (4 days × 2 engineers)

---

### Phase 1 Exit Criteria

**Definition of Done:**
- [ ] All endpoints functional and documented in Swagger
- [ ] Authentication and authorization working
- [ ] Rate limiting configured
- [ ] Error handling consistent across all endpoints
- [ ] Integration tests passing (≥80% coverage)
- [ ] API can be deployed to staging environment
- [ ] Performance: p95 latency < 500ms for all endpoints

**Deliverables:**
- ✅ `Hazina.API.Search` project
- ✅ 15+ REST endpoints (search, documents, health)
- ✅ OpenAPI specification (swagger.json)
- ✅ Integration test suite
- ✅ Deployment guide

**Risks & Mitigations:**
| Risk | Impact | Mitigation |
|------|--------|------------|
| JWT library breaking changes | Medium | Pin versions, test thoroughly |
| Rate limiting too aggressive | Low | Make limits configurable |
| File upload size limits | Medium | Configure limits per environment |

---

## Phase 2: NLP Enhancements (Weeks 3-4)

**Goal:** Add key phrase extraction, sentiment analysis, and advanced OCR to enrich document processing.

**Duration:** 2 weeks (40 work hours per engineer)

**Team:** 2 engineers
- Engineer A: Key phrase extraction, sentiment analysis
- Engineer B: OCR integration, document processor updates

---

### Task 2.1: Key Phrase Extraction (Days 11-13)

**Subtasks:**
1. Create `Hazina.AI.KeyPhraseExtraction` project
2. Implement `IKeyPhraseExtractor` interface
3. Implement `LLMKeyPhraseExtractor` (uses existing LLM providers)
4. Implement `TfIdfKeyPhraseExtractor` (statistical)
5. Implement `HybridKeyPhraseExtractor` (combines both)
6. Add configuration options
7. Write unit tests
8. Integrate with `DocumentStore`
9. Add API endpoint: `POST /api/v1/documents/{id}/keyphrases`
10. Add automatic key phrase extraction to document upload

**Dependencies:** Phase 1 complete

**Deliverables:**
- ✅ `Hazina.AI.KeyPhraseExtraction` NuGet package
- ✅ 3 extractor implementations (LLM, TF-IDF, Hybrid)
- ✅ Automatic key phrase tagging on document upload
- ✅ API endpoint to manually extract key phrases

**Code:**
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
    public double Score { get; set; }
    public int Frequency { get; set; }
}

// Integration with DocumentStore
public class EnhancedDocumentProcessor
{
    private readonly IKeyPhraseExtractor _keyPhraseExtractor;

    public async Task<DocumentMetadata> ProcessAsync(string content)
    {
        var metadata = await base.ProcessAsync(content);

        // Extract key phrases
        var keyPhrases = await _keyPhraseExtractor.ExtractAsync(
            content,
            new KeyPhraseExtractionOptions { MaxPhrases = 15 }
        );

        // Add as tags
        metadata.Tags.AddRange(keyPhrases
            .Where(kp => kp.Score > 0.5)
            .Select(kp => kp.Phrase));

        return metadata;
    }
}
```

**API Endpoint:**
```http
POST /api/v1/documents/{id}/keyphrases
{
  "maxPhrases": 20,
  "strategy": "hybrid"
}

Response:
{
  "documentId": "doc_123",
  "keyPhrases": [
    { "phrase": "machine learning", "score": 0.95, "frequency": 12 },
    { "phrase": "neural networks", "score": 0.87, "frequency": 8 }
  ]
}
```

**Acceptance Criteria:**
- [ ] Upload document, key phrases automatically extracted and added as tags
- [ ] `POST /api/v1/documents/{id}/keyphrases` returns top 20 key phrases
- [ ] LLM extractor works with OpenAI, Anthropic, Gemini
- [ ] TF-IDF extractor works without LLM (fast fallback)
- [ ] Hybrid extractor produces better results than either alone
- [ ] Unit tests: ≥90% coverage

**Effort:** 24 hours (3 days × 2 engineers)

---

### Task 2.2: Sentiment Analysis (Days 14-16)

**Subtasks:**
1. Create `Hazina.AI.SentimentAnalysis` project
2. Implement `ISentimentAnalyzer` interface
3. Implement `LLMSentimentAnalyzer`
4. Implement `VaderSentimentAnalyzer` (lexicon-based)
5. Implement `AspectBasedSentimentAnalyzer`
6. Add emotion detection (joy, anger, sadness, fear, etc.)
7. Write unit tests
8. Integrate with `DocumentStore`
9. Add API endpoint: `POST /api/v1/documents/{id}/sentiment`
10. Add sentiment as searchable metadata

**Dependencies:** Phase 1 complete

**Deliverables:**
- ✅ `Hazina.AI.SentimentAnalysis` NuGet package
- ✅ 2 analyzer implementations (LLM, VADER)
- ✅ Aspect-based sentiment analysis
- ✅ Automatic sentiment scoring on document upload
- ✅ Search filtering by sentiment

**Code:**
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
    public double PositiveScore { get; set; }
    public double NegativeScore { get; set; }
    public double NeutralScore { get; set; }
    public List<Emotion> Emotions { get; set; }
}

// Integration
var sentiment = await _sentimentAnalyzer.AnalyzeAsync(doc.Content);
doc.Metadata["sentiment_polarity"] = sentiment.Polarity.ToString();
doc.Metadata["sentiment_score"] = (sentiment.PositiveScore - sentiment.NegativeScore).ToString();
doc.Tags.Add($"sentiment_{sentiment.Polarity.ToString().ToLower()}");
```

**API Endpoint:**
```http
POST /api/v1/documents/{id}/sentiment
{
  "analyzeAspects": true,
  "aspects": ["UI", "performance", "security"]
}

Response:
{
  "documentId": "doc_123",
  "overall": {
    "polarity": "positive",
    "positiveScore": 0.78,
    "negativeScore": 0.12,
    "neutralScore": 0.10,
    "emotions": [
      { "type": "joy", "intensity": 0.65 },
      { "type": "trust", "intensity": 0.54 }
    ]
  },
  "aspects": [
    {
      "aspect": "UI",
      "polarity": "positive",
      "score": 0.82,
      "evidence": "The user interface is intuitive and well-designed"
    }
  ]
}
```

**Search with Sentiment Filter:**
```http
GET /api/v1/documents?sentiment=positive&tags=customer-feedback
```

**Acceptance Criteria:**
- [ ] Upload document, sentiment automatically analyzed
- [ ] `POST /api/v1/documents/{id}/sentiment` returns sentiment breakdown
- [ ] Aspect-based sentiment works (e.g., "UI is great but performance is poor")
- [ ] Emotion detection identifies ≥5 emotion types
- [ ] Search filtering by sentiment works
- [ ] VADER analyzer works without LLM (fast fallback)

**Effort:** 24 hours (3 days × 2 engineers)

---

### Task 2.3: Advanced OCR Integration (Days 17-20)

**Subtasks:**
1. Create `Hazina.AI.OCR` project
2. Implement `IOCREngine` interface
3. Implement `TesseractOCREngine` (open-source)
4. Implement `AzureComputerVisionOCR` (cloud)
5. Implement `HybridOCREngine` (Tesseract first, cloud fallback)
6. Add image preprocessing (grayscale, contrast, deskew)
7. Add PDF page-to-image conversion
8. Write unit tests
9. Integrate with `BinaryDocumentProcessor`
10. Add API endpoint: `POST /api/v1/ocr/extract`
11. Update document upload to use OCR for images/PDFs

**Dependencies:** Phase 1 complete

**Deliverables:**
- ✅ `Hazina.AI.OCR` NuGet package
- ✅ 3 OCR engine implementations (Tesseract, Azure, Hybrid)
- ✅ Automatic OCR on image/PDF upload
- ✅ API endpoint for manual OCR extraction
- ✅ Multi-page PDF support

**Code:**
```csharp
public interface IOCREngine
{
    Task<OCRResult> ExtractTextAsync(byte[] imageData, OCROptions options = null);
    Task<OCRResult> ExtractTextFromPdfAsync(byte[] pdfData, OCROptions options = null);
}

public class OCRResult
{
    public string FullText { get; set; }
    public List<TextBlock> TextBlocks { get; set; }
    public double Confidence { get; set; }
    public string Language { get; set; }
}

// Hybrid strategy
public class HybridOCREngine : IOCREngine
{
    public async Task<OCRResult> ExtractTextAsync(byte[] imageData, OCROptions options = null)
    {
        // Try Tesseract first (fast, local)
        var tesseractResult = await _tesseract.ExtractTextAsync(imageData, options);

        if (tesseractResult.Confidence >= 0.8)
            return tesseractResult;

        // Fallback to Azure (slower, more accurate)
        return await _azure.ExtractTextAsync(imageData, options);
    }
}
```

**API Endpoint:**
```http
POST /api/v1/ocr/extract
Content-Type: multipart/form-data

file: <image or PDF>
language: eng
preprocessImage: true

Response:
{
  "fullText": "Extracted text content...",
  "confidence": 0.94,
  "language": "eng",
  "textBlocks": [
    {
      "text": "Header Text",
      "confidence": 0.97,
      "boundingBox": { "x": 10, "y": 20, "width": 200, "height": 30 },
      "pageNumber": 1
    }
  ]
}
```

**Integration:**
```csharp
// In BinaryDocumentProcessor
var ocrResult = await _ocrEngine.ExtractTextAsync(imageData);
metadata.SearchableText = ocrResult.FullText;
metadata.CustomMetadata["ocr_confidence"] = ocrResult.Confidence.ToString();
```

**Acceptance Criteria:**
- [ ] Upload scanned PDF, text is extracted and searchable
- [ ] Tesseract extracts text from clean images (≥0.9 confidence)
- [ ] Azure fallback activates for low-confidence Tesseract results
- [ ] Multi-page PDF support (extract text from all pages)
- [ ] Image preprocessing improves OCR accuracy by ≥15%
- [ ] `POST /api/v1/ocr/extract` processes image in <3 seconds

**Effort:** 32 hours (4 days × 2 engineers)

---

### Phase 2 Exit Criteria

**Definition of Done:**
- [ ] Key phrase extraction working with ≥3 strategies
- [ ] Sentiment analysis working with ≥2 analyzers
- [ ] OCR working with Tesseract and Azure
- [ ] All NLP features integrated with document upload
- [ ] API endpoints for manual invocation
- [ ] Unit tests: ≥85% coverage
- [ ] Integration tests for end-to-end flows
- [ ] Documentation updated

**Deliverables:**
- ✅ 3 new NuGet packages (KeyPhraseExtraction, SentimentAnalysis, OCR)
- ✅ 6 new API endpoints
- ✅ Enhanced document processing pipeline
- ✅ Test suite

**Risks & Mitigations:**
| Risk | Impact | Mitigation |
|------|--------|------------|
| Tesseract accuracy lower than expected | Medium | Use hybrid approach with cloud fallback |
| LLM costs for sentiment analysis | Medium | Use VADER as default, LLM as opt-in |
| Key phrase extraction slow | Low | Cache results, use hybrid strategy |

---

## Phase 3: GraphQL API (Weeks 5-6)

**Goal:** Add GraphQL API for flexible, client-driven queries and real-time subscriptions.

**Duration:** 2 weeks (40 work hours per engineer)

**Team:** 2 engineers
- Engineer A: Schema design, query resolvers
- Engineer B: Mutation resolvers, subscriptions, data loaders

---

### Task 3.1: Schema Design & Project Setup (Days 21-22)

**Subtasks:**
1. Create `Hazina.API.GraphQL` project
2. Add HotChocolate dependencies
3. Design GraphQL schema (types, queries, mutations)
4. Define root `Query` type
5. Define root `Mutation` type
6. Define root `Subscription` type
7. Add GraphQL Playground
8. Configure in-memory query cache
9. Set up DataLoader for N+1 prevention
10. Write schema documentation

**Dependencies:** Phase 1 complete

**Deliverables:**
- ✅ `Hazina.API.GraphQL` project
- ✅ Complete GraphQL schema definition
- ✅ GraphQL Playground accessible
- ✅ Schema documentation

**Schema:**
```graphql
type Query {
  # Search operations
  search(
    query: String!
    topK: Int = 10
    minSimilarity: Float = 0.7
  ): SearchResult!

  # Document operations
  document(id: ID!): Document
  documents(
    page: Int = 1
    pageSize: Int = 20
    filter: DocumentFilter
  ): DocumentConnection!

  # Graph operations
  entities(filter: EntityFilter, limit: Int = 50): [Entity!]!
  entity(id: ID!): Entity
  entityRelationships(
    entityId: ID!
    relationshipType: String
  ): [Relationship!]!
}

type Mutation {
  uploadDocument(input: UploadDocumentInput!): Document!
  updateDocument(id: ID!, input: UpdateDocumentInput!): Document!
  deleteDocument(id: ID!): Boolean!
  extractKeyPhrases(documentId: ID!, maxPhrases: Int = 20): [KeyPhrase!]!
  analyzeSentiment(documentId: ID!): SentimentResult!
}

type Subscription {
  documentUploaded: Document!
  documentUpdated(id: ID!): Document!
  searchCompleted: SearchResult!
}

type SearchResult {
  answer: String!
  confidence: Float!
  sources: [Document!]!
  reasoningPath: [String!]!
  metadata: SearchMetadata!
}

type Document {
  id: ID!
  title: String!
  content: String!
  metadata: Metadata!
  tags: [String!]!
  createdAt: DateTime!
  modifiedAt: DateTime!
  similar(topK: Int = 5): [Document!]!
  keyPhrases(maxPhrases: Int = 20): [KeyPhrase!]!
  sentiment: SentimentResult
}

type Entity {
  id: ID!
  name: String!
  type: EntityType!
  properties: JSON
  documents: [Document!]!
  relationships: [Relationship!]!
}

# ... (full schema in actual implementation)
```

**Acceptance Criteria:**
- [ ] GraphQL Playground accessible at `/graphql`
- [ ] Schema introspection works
- [ ] All types documented with descriptions
- [ ] Schema validates with no errors

**Effort:** 16 hours (2 days × 2 engineers)

---

### Task 3.2: Query & Mutation Resolvers (Days 23-25)

**Subtasks:**
1. Implement `Query.search` resolver
2. Implement `Query.document` and `Query.documents` resolvers
3. Implement `Query.entities` and `Query.entity` resolvers
4. Implement `Mutation.uploadDocument` resolver
5. Implement `Mutation.updateDocument` resolver
6. Implement `Mutation.deleteDocument` resolver
7. Add field resolvers for nested data (e.g., `Document.similar`)
8. Implement DataLoaders to prevent N+1 queries
9. Add error handling
10. Write integration tests

**Dependencies:** Task 3.1

**Deliverables:**
- ✅ All query resolvers implemented
- ✅ All mutation resolvers implemented
- ✅ DataLoaders for batching
- ✅ Error handling

**Code:**
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

    public async Task<Document?> GetDocumentAsync(
        string id,
        [Service] IDocumentStore documentStore,
        DocumentByIdDataLoader dataLoader)
    {
        return await dataLoader.LoadAsync(id);
    }
}

public class Mutation
{
    public async Task<Document> UploadDocumentAsync(
        UploadDocumentInput input,
        [Service] IDocumentStore documentStore)
    {
        var document = await documentStore.StoreFromBytesAsync(
            input.FileName,
            input.Content,
            input.MimeType,
            input.Tags
        );

        return MapToDocument(document);
    }
}

// DataLoader to prevent N+1
public class DocumentByIdDataLoader : BatchDataLoader<string, Document>
{
    private readonly IDocumentStore _documentStore;

    protected override async Task<IReadOnlyDictionary<string, Document>> LoadBatchAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
    {
        var documents = await _documentStore.GetByIdsAsync(keys);
        return documents.ToDictionary(d => d.Id, MapToDocument);
    }
}
```

**Example Query:**
```graphql
query GetDocumentWithSimilar {
  document(id: "doc_123") {
    id
    title
    content
    tags
    similar(topK: 5) {
      id
      title
      relevanceScore
    }
    keyPhrases(maxPhrases: 10) {
      phrase
      score
    }
    sentiment {
      polarity
      positiveScore
      emotions {
        type
        intensity
      }
    }
  }
}
```

**Acceptance Criteria:**
- [ ] `query { search(...) }` returns results
- [ ] `query { document(id: "...") }` returns document
- [ ] Nested queries work (e.g., `document.similar.keyPhrases`)
- [ ] DataLoaders batch requests (verify logs show 1 DB query, not N)
- [ ] Mutations create/update/delete documents
- [ ] Errors return GraphQL error format

**Effort:** 24 hours (3 days × 2 engineers)

---

### Task 3.3: Real-Time Subscriptions (Days 26-28)

**Subtasks:**
1. Configure WebSocket support
2. Implement `Subscription.documentUploaded`
3. Implement `Subscription.documentUpdated`
4. Implement `Subscription.searchCompleted`
5. Add event publishing from API
6. Add subscription filtering
7. Write subscription integration tests
8. Add client example (JavaScript)

**Dependencies:** Task 3.2

**Deliverables:**
- ✅ WebSocket endpoint for subscriptions
- ✅ 3 subscription types working
- ✅ Event publishing from REST API
- ✅ Client example

**Code:**
```csharp
public class Subscription
{
    [Subscribe]
    public Document DocumentUploaded(
        [EventMessage] Document document)
    {
        return document;
    }

    [Subscribe]
    [Topic("{id}")]
    public Document DocumentUpdated(
        string id,
        [EventMessage] Document document)
    {
        return document;
    }
}

// Publishing events
public class DocumentsController : ControllerBase
{
    private readonly ITopicEventSender _eventSender;

    [HttpPost("upload")]
    public async Task<ActionResult<DocumentResponse>> UploadAsync(IFormFile file)
    {
        var document = await _documentStore.StoreFromBytesAsync(...);

        // Publish event for subscribers
        await _eventSender.SendAsync(
            nameof(Subscription.DocumentUploaded),
            MapToDocument(document)
        );

        return CreatedAtAction(...);
    }
}
```

**Client Example (JavaScript):**
```javascript
import { createClient } from 'graphql-ws';

const client = createClient({
  url: 'ws://localhost:5001/graphql',
});

const subscription = client.subscribe(
  {
    query: `
      subscription {
        documentUploaded {
          id
          title
          createdAt
        }
      }
    `,
  },
  {
    next: (data) => {
      console.log('New document uploaded:', data.documentUploaded);
    },
    error: (error) => {
      console.error('Subscription error:', error);
    },
    complete: () => {
      console.log('Subscription completed');
    },
  }
);
```

**Acceptance Criteria:**
- [ ] Upload document via REST API, subscription receives event
- [ ] Multiple clients can subscribe simultaneously
- [ ] Subscription filtering works (e.g., only documents with tag "research")
- [ ] WebSocket connection stays alive with heartbeat
- [ ] Client example successfully receives events

**Effort:** 24 hours (3 days × 2 engineers)

---

### Phase 3 Exit Criteria

**Definition of Done:**
- [ ] GraphQL API fully functional
- [ ] All queries, mutations, subscriptions working
- [ ] DataLoaders prevent N+1 queries
- [ ] Real-time subscriptions via WebSocket
- [ ] Client example demonstrates usage
- [ ] Integration tests: ≥80% coverage
- [ ] Schema documented and published

**Deliverables:**
- ✅ `Hazina.API.GraphQL` project
- ✅ Complete GraphQL schema
- ✅ GraphQL Playground
- ✅ Client SDK or examples
- ✅ Test suite

**Risks & Mitigations:**
| Risk | Impact | Mitigation |
|------|--------|------------|
| WebSocket scaling issues | Medium | Use Redis backplane for multi-instance |
| Subscription memory leaks | High | Implement connection timeout, monitoring |
| Complex nested queries slow | Medium | Add query complexity limits |

---

## Phase 4: Advanced Features (Weeks 7-8)

**Goal:** Add search analytics, performance optimization, and production readiness.

**Duration:** 2 weeks (40 work hours per engineer)

**Team:** 2 engineers
- Engineer A: Search analytics, monitoring
- Engineer B: Performance optimization, deployment

---

### Task 4.1: Search Analytics (Days 29-31)

**Subtasks:**
1. Create analytics database schema (queries, clicks, results)
2. Implement query logging
3. Implement click tracking
4. Add analytics API endpoints
5. Build analytics dashboard (simple HTML/JS)
6. Add popular queries report
7. Add zero-result queries report
8. Add search performance metrics
9. Write tests

**Dependencies:** Phase 1 complete

**Deliverables:**
- ✅ Search analytics database
- ✅ Query logging and click tracking
- ✅ Analytics API endpoints
- ✅ Simple dashboard

**Schema:**
```sql
CREATE TABLE SearchQueries (
    Id TEXT PRIMARY KEY,
    Query TEXT NOT NULL,
    UserId TEXT,
    Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    ResultCount INTEGER,
    TopResultId TEXT,
    ClickedResultId TEXT,
    ResponseTimeMs INTEGER
);

CREATE INDEX idx_queries_timestamp ON SearchQueries(Timestamp);
CREATE INDEX idx_queries_user ON SearchQueries(UserId);
```

**API Endpoints:**
```http
GET /api/v1/analytics/queries/popular?days=7
GET /api/v1/analytics/queries/zero-results?days=7
GET /api/v1/analytics/performance?days=7
POST /api/v1/analytics/clicks
```

**Acceptance Criteria:**
- [ ] Every search query is logged
- [ ] Popular queries report shows top 20 queries in last 7 days
- [ ] Zero-result queries report identifies queries with no results
- [ ] Performance metrics show p50, p95, p99 latencies
- [ ] Dashboard displays analytics in browser

**Effort:** 24 hours (3 days × 2 engineers)

---

### Task 4.2: Performance Optimization (Days 32-35)

**Subtasks:**
1. Add Redis caching for hot queries
2. Optimize database indexes
3. Add response compression (gzip, brotli)
4. Implement query result pagination
5. Add query complexity limits (GraphQL)
6. Profile and optimize slow queries
7. Add request batching
8. Load testing with k6 or Locust
9. Tune connection pooling
10. Add CDN support for static responses

**Dependencies:** All previous phases

**Deliverables:**
- ✅ Redis caching for embeddings and results
- ✅ Database indexes optimized
- ✅ Load test results (≥1000 req/sec)
- ✅ p95 latency < 200ms

**Code:**
```csharp
// Redis caching
public class CachedRAGEngine
{
    private readonly RAGEngine _ragEngine;
    private readonly IDistributedCache _cache;

    public async Task<RAGResult> QueryAsync(string query, RAGQueryOptions options)
    {
        var cacheKey = $"rag:{ComputeHash(query)}:{options.TopK}";

        var cachedResult = await _cache.GetStringAsync(cacheKey);
        if (cachedResult != null)
        {
            return JsonSerializer.Deserialize<RAGResult>(cachedResult);
        }

        var result = await _ragEngine.QueryAsync(query, options);

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            }
        );

        return result;
    }
}
```

**Load Testing:**
```javascript
// k6 load test
import http from 'k6/http';
import { check } from 'k6';

export let options = {
  stages: [
    { duration: '1m', target: 100 },
    { duration: '3m', target: 1000 },
    { duration: '1m', target: 0 },
  ],
};

export default function () {
  const payload = JSON.stringify({
    query: 'What is OAuth2?',
    topK: 10,
  });

  const res = http.post('http://localhost:5001/api/v1/search/query', payload, {
    headers: { 'Content-Type': 'application/json' },
  });

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 500ms': (r) => r.timings.duration < 500,
  });
}
```

**Acceptance Criteria:**
- [ ] Load test sustains 1000 req/sec
- [ ] p95 latency < 200ms under load
- [ ] Cache hit rate ≥60% for repeated queries
- [ ] Response compression reduces payload size by ≥70%
- [ ] No memory leaks after 1-hour load test

**Effort:** 32 hours (4 days × 2 engineers)

---

### Task 4.3: Documentation & Deployment (Days 36-40)

**Subtasks:**
1. Write API usage guide
2. Write deployment guide (Docker, Kubernetes)
3. Create Dockerfile for API
4. Create Docker Compose for full stack
5. Create Kubernetes manifests
6. Write environment configuration guide
7. Create client SDK examples (C#, Python, JavaScript)
8. Set up CI/CD pipeline (GitHub Actions or Azure DevOps)
9. Write troubleshooting guide
10. Conduct final review and testing

**Dependencies:** All previous phases

**Deliverables:**
- ✅ Complete API documentation
- ✅ Deployment guides
- ✅ Docker and Kubernetes configs
- ✅ Client SDK examples
- ✅ CI/CD pipeline

**Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Hazina.API.Search/Hazina.API.Search.csproj", "Hazina.API.Search/"]
RUN dotnet restore "Hazina.API.Search/Hazina.API.Search.csproj"
COPY . .
WORKDIR "/src/Hazina.API.Search"
RUN dotnet build "Hazina.API.Search.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Hazina.API.Search.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Hazina.API.Search.dll"]
```

**Docker Compose:**
```yaml
version: '3.8'

services:
  api:
    build: .
    ports:
      - "5001:80"
    environment:
      - ConnectionStrings__Postgres=Host=postgres;Database=hazina;Username=hazina;Password=secure
      - ConnectionStrings__Redis=redis:6379
    depends_on:
      - postgres
      - redis

  postgres:
    image: pgvector/pgvector:pg16
    environment:
      - POSTGRES_USER=hazina
      - POSTGRES_PASSWORD=secure
      - POSTGRES_DB=hazina
    volumes:
      - postgres-data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    volumes:
      - redis-data:/data

volumes:
  postgres-data:
  redis-data:
```

**Kubernetes Deployment:**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: hazina-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: hazina-api
  template:
    metadata:
      labels:
        app: hazina-api
    spec:
      containers:
      - name: api
        image: hazina/api-search:latest
        ports:
        - containerPort: 80
        env:
        - name: ConnectionStrings__Postgres
          valueFrom:
            secretKeyRef:
              name: hazina-secrets
              key: postgres-connection
        - name: ConnectionStrings__Redis
          valueFrom:
            secretKeyRef:
              name: hazina-secrets
              key: redis-connection
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "2Gi"
            cpu: "2000m"
```

**Acceptance Criteria:**
- [ ] Docker image builds successfully
- [ ] Docker Compose brings up full stack
- [ ] Kubernetes deployment works on test cluster
- [ ] API documentation complete and accessible
- [ ] Client examples work (C#, Python, JavaScript)
- [ ] CI/CD pipeline deploys to staging on commit

**Effort:** 40 hours (5 days × 2 engineers)

---

### Phase 4 Exit Criteria

**Definition of Done:**
- [ ] Search analytics functional
- [ ] Performance targets met (1000 req/sec, p95 < 200ms)
- [ ] Complete documentation published
- [ ] Docker and Kubernetes deployment working
- [ ] CI/CD pipeline operational
- [ ] Production readiness review passed

**Deliverables:**
- ✅ Analytics dashboard
- ✅ Load test results
- ✅ Deployment artifacts (Docker, K8s)
- ✅ Complete documentation
- ✅ CI/CD pipeline

**Risks & Mitigations:**
| Risk | Impact | Mitigation |
|------|--------|------------|
| Performance targets not met | High | Profile early, optimize incrementally |
| Kubernetes deployment complex | Medium | Use managed K8s (AKS, GKE, EKS) |
| Documentation incomplete | Low | Allocate dedicated time, assign owner |

---

## Project Timeline

```
Week 1-2: Phase 1 - REST API Foundation
├── Days 1-2:   Project setup & infrastructure
├── Days 3-4:   Core search endpoints
├── Days 5-6:   Document endpoints
└── Days 7-10:  Authentication & security

Week 3-4: Phase 2 - NLP Enhancements
├── Days 11-13: Key phrase extraction
├── Days 14-16: Sentiment analysis
└── Days 17-20: Advanced OCR integration

Week 5-6: Phase 3 - GraphQL API
├── Days 21-22: Schema design & setup
├── Days 23-25: Query & mutation resolvers
└── Days 26-28: Real-time subscriptions

Week 7-8: Phase 4 - Advanced Features
├── Days 29-31: Search analytics
├── Days 32-35: Performance optimization
└── Days 36-40: Documentation & deployment

Total: 40 working days (8 weeks)
```

---

## Resource Requirements

### Team
- **2 Senior Engineers** (full-time, 8 weeks)
- **1 Tech Lead** (part-time, reviews and architecture decisions)
- **1 QA Engineer** (part-time, weeks 4-8 for testing)

### Infrastructure
- **Development:**
  - Local PostgreSQL with pgvector
  - Local Redis
  - Azure/OpenAI API keys for testing

- **Staging:**
  - Kubernetes cluster (3 nodes)
  - PostgreSQL (managed service)
  - Redis (managed service)
  - Load balancer

- **Production:**
  - Kubernetes cluster (5+ nodes)
  - PostgreSQL (replicated)
  - Redis (clustered)
  - CDN
  - Monitoring (Prometheus, Grafana)

### Budget Estimate
| Item | Cost |
|------|------|
| Engineering (2 seniors × 8 weeks) | $80,000 |
| Infrastructure (staging + production) | $5,000/month |
| LLM API costs (OpenAI, Anthropic) | $1,000/month |
| Total (8 weeks) | ~$92,000 |

---

## Success Metrics

### Phase 1 Success Metrics
- [ ] API response time: p95 < 500ms
- [ ] API uptime: ≥99%
- [ ] Test coverage: ≥80%
- [ ] API documentation completeness: 100%

### Phase 2 Success Metrics
- [ ] Key phrase extraction accuracy: ≥85% (human eval)
- [ ] Sentiment analysis accuracy: ≥80% (vs. labeled dataset)
- [ ] OCR accuracy: ≥90% (on clean images)
- [ ] Processing time per document: <10 seconds

### Phase 3 Success Metrics
- [ ] GraphQL query complexity limit: 1000
- [ ] Subscription latency: <100ms
- [ ] DataLoader reduces queries by ≥90%
- [ ] Schema completeness: 100% (all features exposed)

### Phase 4 Success Metrics
- [ ] Load test throughput: ≥1000 req/sec
- [ ] p95 latency under load: <200ms
- [ ] Cache hit rate: ≥60%
- [ ] Zero-downtime deployment: Yes

---

## Risk Management

### High-Priority Risks

| Risk | Probability | Impact | Mitigation Strategy |
|------|-------------|--------|---------------------|
| Performance targets not met | Medium | High | Early profiling, incremental optimization, fallback to simpler approaches |
| LLM API costs exceed budget | Medium | Medium | Use caching, fallback to statistical methods, set rate limits |
| OCR accuracy lower than expected | Low | Medium | Hybrid approach (Tesseract + cloud), image preprocessing |
| Scope creep | High | Medium | Strict phase boundaries, defer non-essential features |
| Integration complexity | Medium | High | Incremental integration, comprehensive testing |

### Medium-Priority Risks

| Risk | Probability | Impact | Mitigation Strategy |
|------|-------------|--------|---------------------|
| Dependency breaking changes | Low | Medium | Pin versions, test before upgrading |
| Security vulnerabilities | Low | High | Security scanning in CI/CD, regular updates |
| Documentation lag | Medium | Low | Allocate dedicated documentation time |
| Testing gaps | Medium | Medium | Require tests for all PRs, track coverage |

---

## Dependencies

### External Dependencies
- **NuGet Packages:**
  - ASP.NET Core 9.0
  - HotChocolate 13+
  - Tesseract.NET
  - Azure.AI.Vision
  - AspNetCoreRateLimit
  - Serilog
  - StackExchange.Redis

- **Cloud Services (Optional):**
  - Azure Computer Vision API
  - OpenAI API
  - Anthropic API

### Internal Dependencies
- **Existing Hazina Packages:**
  - Hazina.Store.DocumentStore
  - Hazina.Store.EmbeddingStore
  - Hazina.AI.RAG
  - Hazina.LLMs.*

---

## Testing Strategy

### Unit Tests
- **Coverage Target:** ≥85%
- **Tools:** xUnit, Moq
- **Scope:** All services, controllers, resolvers

### Integration Tests
- **Coverage Target:** ≥75%
- **Tools:** WebApplicationFactory, Testcontainers
- **Scope:** API endpoints, database interactions, LLM integrations

### Load Tests
- **Tools:** k6, Locust
- **Scope:** All API endpoints
- **Targets:**
  - 1000 req/sec sustained
  - p95 latency < 200ms
  - No memory leaks over 1 hour

### Security Tests
- **Tools:** OWASP ZAP, Burp Suite
- **Scope:** Authentication, authorization, input validation
- **Targets:**
  - No critical vulnerabilities
  - No secrets in logs or responses

---

## Deployment Strategy

### Environments
1. **Development:** Local Docker Compose
2. **Staging:** Kubernetes (AKS/GKE/EKS)
3. **Production:** Kubernetes (multi-region)

### CI/CD Pipeline
```
Code Push → GitHub
    ↓
GitHub Actions Trigger
    ↓
┌────────────────────────┐
│ Build & Test           │
│ - dotnet build         │
│ - dotnet test          │
│ - Security scan        │
└────────────────────────┘
    ↓
┌────────────────────────┐
│ Build Docker Image     │
│ - docker build         │
│ - Push to registry     │
└────────────────────────┘
    ↓
┌────────────────────────┐
│ Deploy to Staging      │
│ - kubectl apply        │
│ - Run smoke tests      │
└────────────────────────┘
    ↓
Manual Approval
    ↓
┌────────────────────────┐
│ Deploy to Production   │
│ - Blue-green deploy    │
│ - Smoke tests          │
│ - Rollback if needed   │
└────────────────────────┘
```

---

## Conclusion

This roadmap provides a **clear, phased approach** to completing Hazina's cognitive search capabilities. By following this plan, the team will deliver:

1. **Production-ready REST API** (Weeks 1-2)
2. **Enhanced NLP capabilities** (Weeks 3-4)
3. **Flexible GraphQL API** (Weeks 5-6)
4. **Performance-optimized, production-ready platform** (Weeks 7-8)

**Key Success Factors:**
- Incremental delivery (working software each phase)
- Comprehensive testing (unit, integration, load)
- Clear success metrics (performance, accuracy, coverage)
- Risk mitigation strategies
- Production-ready deployment artifacts

**Expected Outcome:** A **100% complete cognitive search platform** ready for enterprise deployment, competitive with Azure Cognitive Search and Elasticsearch.

---

**Document Status:** Complete
**Next Steps:** Review and approve, begin Phase 1 implementation
**Dependencies:** STATUS.md (analysis), DESIGN.md (architecture)
