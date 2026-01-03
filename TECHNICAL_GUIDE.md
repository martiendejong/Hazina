# Hazina Technical Guide

## Building RAG-First AI Applications with .NET Core

**For Architects, Senior Developers, and Technical Decision Makers**

---

## Introduction

This guide demonstrates how to rapidly deploy a production-ready RAG (Retrieval-Augmented Generation) platform using Hazina with .NET Core and Entity Framework Identity. You'll see real code examples showing how Hazina reduces months of infrastructure development to days.

### What You'll Learn

1. Setting up multi-provider AI with automatic failover in 4 lines
2. Building a RAG pipeline with hallucination detection
3. Integrating with ASP.NET Core Identity for user-scoped AI
4. Creating autonomous agents with tool calling
5. Production patterns for enterprise deployment

---

## Part 1: The 4-Line Revolution

### Traditional AI Integration (The Hard Way)

```csharp
// Traditional approach: 70+ lines of boilerplate

// 1. Configure OpenAI
var openaiConfig = new OpenAIConfig
{
    ApiKey = "sk-...",
    Model = "gpt-4o-mini"
};
var openaiClient = new OpenAIClientWrapper(openaiConfig);

// 2. Configure Anthropic as backup
var claudeConfig = new AnthropicConfig
{
    ApiKey = "sk-ant-...",
    Model = "claude-3-5-sonnet-20241022"
};
var claudeClient = new ClaudeClientWrapper(claudeConfig);

// 3. Setup provider registry
var registry = new ProviderRegistry();
registry.Register("openai", openaiClient, new ProviderMetadata
{
    Priority = 1,
    CostPer1KTokens = 0.03m
});
registry.Register("anthropic", claudeClient, new ProviderMetadata
{
    Priority = 2,
    CostPer1KTokens = 0.015m
});

// 4. Setup health monitoring
var healthMonitor = new ProviderHealthMonitor(registry);

// 5. Setup cost tracking
var costTracker = new CostTracker();
costTracker.SetBudgetLimit(100m, BudgetPeriod.Daily);

// 6. Setup provider selection
var selector = new ProviderSelector(registry);

// 7. Setup failover handling
var failoverHandler = new FailoverHandler(registry, selector, healthMonitor);

// 8. Create orchestrator
var orchestrator = new ProviderOrchestrator(
    registry,
    healthMonitor,
    costTracker,
    selector,
    failoverHandler
);

// 9. Setup fault detection components
var validator = new BasicResponseValidator();
var hallucinationDetector = new BasicHallucinationDetector();
var errorPatternRecognizer = new BasicErrorPatternRecognizer();
var confidenceScorer = new BasicConfidenceScorer();

// 10. Create fault handler
var faultHandler = new AdaptiveFaultHandler(
    orchestrator,
    validator,
    hallucinationDetector,
    errorPatternRecognizer,
    confidenceScorer
);

// 11. Finally, execute
var messages = new List<HazinaChatMessage>
{
    new() { Role = HazinaMessageRole.User, Text = "Hello" }
};
var validationContext = new ValidationContext();
var response = await faultHandler.ExecuteWithFaultDetectionAsync(
    messages,
    validationContext,
    CancellationToken.None
);
```

**Result:** 70+ lines, complex setup, easy to misconfigure.

### Hazina Approach (The Smart Way)

```csharp
// Hazina approach: 4 lines

// 1. Setup with automatic failover
QuickSetup.SetupAndConfigure(
    openAIKey: "sk-...",
    anthropicKey: "sk-ant-..."
);

// 2. Execute with fault detection
var result = await Hazina.AskSafeAsync("Hello", confidence: 0.9);
```

**Result:** 4 lines, production-ready, impossible to misconfigure.

### Complexity Reduction: 97%

| Aspect | Traditional | Hazina | Reduction |
|--------|-------------|--------|-----------|
| Lines of Code | 70+ | 4 | 94% |
| Components to Configure | 11 | 1 | 91% |
| Failure Points | Many | 0 | 100% |
| Time to Production | Weeks | Minutes | 99% |

---

## Part 2: RAG-First Architecture

### Understanding Hazina's Metadata-First RAG

Unlike vector-only RAG systems that require embeddings for every query, Hazina uses a **metadata-first architecture**:

```
┌─────────────────────────────────────────────────────────────────┐
│                    HAZINA RAG ARCHITECTURE                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   Query Input                                                    │
│        │                                                         │
│        ▼                                                         │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │              METADATA LAYER (Primary)                    │   │
│   │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐      │   │
│   │  │   Tags      │  │  Dates      │  │  Authors    │      │   │
│   │  │   Types     │  │  Versions   │  │  Projects   │      │   │
│   │  └─────────────┘  └─────────────┘  └─────────────┘      │   │
│   │                                                          │   │
│   │  SQL Query: Fast, Deterministic, No Embeddings Needed   │   │
│   └─────────────────────────────────────────────────────────┘   │
│        │                                                         │
│        ▼                                                         │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │            EMBEDDING LAYER (Secondary)                   │   │
│   │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐      │   │
│   │  │   Vectors   │  │  Similarity │  │  Semantic   │      │   │
│   │  │   (pgvector)│  │   Search    │  │  Matching   │      │   │
│   │  └─────────────┘  └─────────────┘  └─────────────┘      │   │
│   │                                                          │   │
│   │  Vector Search: Fuzzy, Semantic, Optional Enhancement   │   │
│   └─────────────────────────────────────────────────────────┘   │
│        │                                                         │
│        ▼                                                         │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │              COMPOSITE SCORING                           │   │
│   │                                                          │   │
│   │  Score = (TagRelevance × 0.3) +                         │   │
│   │          (RecencyScore × 0.2) +                         │   │
│   │          (SemanticSimilarity × 0.4) +                   │   │
│   │          (PositionScore × 0.1)                          │   │
│   │                                                          │   │
│   └─────────────────────────────────────────────────────────┘   │
│        │                                                         │
│        ▼                                                         │
│   Retrieved Documents → LLM → Grounded Response                  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Benefits of Metadata-First

1. **Works Offline** - No embedding API calls needed for basic queries
2. **Deterministic** - SQL queries return consistent results
3. **Fast** - Database indexes beat vector search for filtered queries
4. **Cost-Effective** - Only use embeddings when semantic search adds value
5. **Auditable** - Clear query path for compliance

### Basic RAG Setup

```csharp
using Hazina.AI.RAG;
using Hazina.Store.DocumentStore;
using Hazina.Store.EmbeddingStore;

// 1. Configure storage backends
var embeddingStore = new PostgresEmbeddingStore(connectionString);
var documentStore = new PostgresDocumentStore(connectionString);

// 2. Create RAG engine
var ragEngine = new RAGEngine(
    documentStore: documentStore,
    embeddingStore: embeddingStore,
    orchestrator: orchestrator,
    options: new RAGOptions
    {
        ChunkSize = 512,
        ChunkOverlap = 50,
        TopK = 5,
        MinSimilarity = 0.7,
        UseHybridSearch = true  // Metadata + embeddings
    }
);

// 3. Index documents
await ragEngine.IndexDocumentAsync(new Document
{
    Id = "doc-001",
    Content = "Your document content here...",
    Metadata = new DocumentMetadata
    {
        Title = "Product Manual",
        Author = "Engineering Team",
        Tags = new[] { "product", "manual", "v2.0" },
        CreatedAt = DateTime.UtcNow
    }
});

// 4. Query with RAG
var response = await ragEngine.QueryAsync(
    "How do I configure the widget?",
    cancellationToken
);

Console.WriteLine($"Answer: {response.Answer}");
Console.WriteLine($"Sources: {string.Join(", ", response.Citations)}");
Console.WriteLine($"Confidence: {response.Confidence:P0}");
```

### Advanced RAG with Reranking

```csharp
// Configure retrieval pipeline with reranking
var pipeline = new RetrievalPipeline(
    retriever: new VectorStoreRetriever(embeddingStore, orchestrator),
    reranker: new LlmJudgeReranker(orchestrator)  // LLM-based relevance scoring
);

var ragEngine = new RAGEngine(
    documentStore: documentStore,
    embeddingStore: embeddingStore,
    orchestrator: orchestrator,
    retrievalPipeline: pipeline,
    options: new RAGOptions
    {
        // Retrieve more, then rerank to top K
        InitialRetrievalCount = 20,
        FinalTopK = 5,

        // Composite scoring weights
        TagRelevanceWeight = 0.3,
        RecencyWeight = 0.2,
        SimilarityWeight = 0.4,
        PositionWeight = 0.1
    }
);
```

---

## Part 3: ASP.NET Core + EF Identity Integration

### Project Structure

```
YourProject/
├── YourProject.API/
│   ├── Controllers/
│   │   ├── ChatController.cs
│   │   └── DocumentsController.cs
│   ├── Services/
│   │   ├── AIService.cs
│   │   └── UserDocumentService.cs
│   ├── Data/
│   │   ├── ApplicationDbContext.cs
│   │   └── Migrations/
│   └── Program.cs
├── YourProject.Core/
│   ├── Entities/
│   │   ├── UserDocument.cs
│   │   └── ChatSession.cs
│   └── Interfaces/
└── YourProject.Infrastructure/
    └── Hazina/
        ├── HazinaServiceCollectionExtensions.cs
        └── UserScopedRAGService.cs
```

### Step 1: Entity Models

```csharp
// YourProject.Core/Entities/UserDocument.cs
using Microsoft.AspNetCore.Identity;

public class UserDocument
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string? Summary { get; set; }

    public string[] Tags { get; set; } = Array.Empty<string>();
    public string DocumentType { get; set; } = "general";

    public bool IsIndexed { get; set; }
    public DateTime? IndexedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ChatSession
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public string Title { get; set; } = null!;
    public List<ChatMessage> Messages { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
}

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public ChatSession Session { get; set; } = null!;

    public string Role { get; set; } = null!; // "user" or "assistant"
    public string Content { get; set; } = null!;
    public string[]? Citations { get; set; }
    public double? Confidence { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class ApplicationUser : IdentityUser
{
    public List<UserDocument> Documents { get; set; } = new();
    public List<ChatSession> ChatSessions { get; set; } = new();

    // AI preferences
    public string PreferredModel { get; set; } = "gpt-4o-mini";
    public double MinConfidence { get; set; } = 0.8;
    public int MaxTokensPerRequest { get; set; } = 4000;
}
```

### Step 2: DbContext Configuration

```csharp
// YourProject.API/Data/ApplicationDbContext.cs
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<UserDocument> UserDocuments => Set<UserDocument>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // UserDocument configuration
        builder.Entity<UserDocument>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.DocumentType);
            entity.HasIndex(e => e.IsIndexed);

            entity.Property(e => e.Tags)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                );

            entity.HasOne(e => e.User)
                .WithMany(u => u.Documents)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ChatSession configuration
        builder.Entity<ChatSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.User)
                .WithMany(u => u.ChatSessions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ChatMessage configuration
        builder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);

            entity.Property(e => e.Citations)
                .HasConversion(
                    v => v != null ? string.Join("|||", v) : null,
                    v => v != null ? v.Split("|||", StringSplitOptions.RemoveEmptyEntries) : null
                );

            entity.HasOne(e => e.Session)
                .WithMany(s => s.Messages)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

### Step 3: Hazina Service Registration

```csharp
// YourProject.Infrastructure/Hazina/HazinaServiceCollectionExtensions.cs
using Hazina.AI.FluentAPI;
using Hazina.AI.Providers;
using Hazina.AI.RAG;
using Hazina.AI.FaultDetection;
using Hazina.Store.EmbeddingStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

public static class HazinaServiceCollectionExtensions
{
    public static IServiceCollection AddHazinaAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Configure provider orchestrator (singleton - thread-safe)
        services.AddSingleton<IProviderOrchestrator>(sp =>
        {
            var openAiKey = configuration["Hazina:OpenAI:ApiKey"];
            var anthropicKey = configuration["Hazina:Anthropic:ApiKey"];

            // Setup with automatic failover
            var orchestrator = QuickSetup.SetupWithFailover(
                openAIKey: openAiKey!,
                anthropicKey: anthropicKey
            );

            // Configure global default
            Hazina.ConfigureDefaultOrchestrator(orchestrator);

            return orchestrator;
        });

        // 2. Configure embedding store
        services.AddSingleton<IEmbeddingStore>(sp =>
        {
            var connectionString = configuration.GetConnectionString("Embeddings");
            return new PostgresEmbeddingStore(connectionString!);
        });

        // 3. Configure RAG engine (scoped - per request)
        services.AddScoped<IRAGEngine>(sp =>
        {
            var orchestrator = sp.GetRequiredService<IProviderOrchestrator>();
            var embeddingStore = sp.GetRequiredService<IEmbeddingStore>();
            var dbContext = sp.GetRequiredService<ApplicationDbContext>();

            return new RAGEngine(
                embeddingStore: embeddingStore,
                orchestrator: orchestrator,
                options: new RAGOptions
                {
                    ChunkSize = 512,
                    ChunkOverlap = 50,
                    TopK = 5,
                    MinSimilarity = 0.7,
                    UseHybridSearch = true
                }
            );
        });

        // 4. Configure fault detection (singleton)
        services.AddSingleton<IAdaptiveFaultHandler>(sp =>
        {
            var orchestrator = sp.GetRequiredService<IProviderOrchestrator>();
            return new AdaptiveFaultHandler(
                orchestrator,
                new BasicResponseValidator(),
                new BasicHallucinationDetector(),
                new BasicErrorPatternRecognizer(),
                new BasicConfidenceScorer()
            );
        });

        // 5. Add user-scoped services
        services.AddScoped<IUserDocumentService, UserDocumentService>();
        services.AddScoped<IAIService, AIService>();

        return services;
    }
}
```

### Step 4: User-Scoped RAG Service

```csharp
// YourProject.Infrastructure/Hazina/UserScopedRAGService.cs
using System.Security.Claims;
using Hazina.AI.RAG;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

public interface IUserDocumentService
{
    Task<UserDocument> AddDocumentAsync(string title, string content, string[] tags);
    Task<List<UserDocument>> GetUserDocumentsAsync();
    Task IndexUserDocumentsAsync();
    Task<RAGResponse> QueryUserDocumentsAsync(string query);
}

public class UserDocumentService : IUserDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IRAGEngine _ragEngine;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _userId;

    public UserDocumentService(
        ApplicationDbContext context,
        IRAGEngine ragEngine,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _ragEngine = ragEngine;
        _httpContextAccessor = httpContextAccessor;
        _userId = httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
    }

    public async Task<UserDocument> AddDocumentAsync(
        string title,
        string content,
        string[] tags)
    {
        var document = new UserDocument
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Title = title,
            Content = content,
            Tags = tags,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.UserDocuments.Add(document);
        await _context.SaveChangesAsync();

        // Index immediately
        await IndexDocumentAsync(document);

        return document;
    }

    public async Task<List<UserDocument>> GetUserDocumentsAsync()
    {
        return await _context.UserDocuments
            .Where(d => d.UserId == _userId)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync();
    }

    public async Task IndexUserDocumentsAsync()
    {
        var unindexedDocs = await _context.UserDocuments
            .Where(d => d.UserId == _userId && !d.IsIndexed)
            .ToListAsync();

        foreach (var doc in unindexedDocs)
        {
            await IndexDocumentAsync(doc);
        }
    }

    private async Task IndexDocumentAsync(UserDocument document)
    {
        // Index with user-scoped metadata
        await _ragEngine.IndexDocumentAsync(new Document
        {
            Id = document.Id.ToString(),
            Content = document.Content,
            Metadata = new DocumentMetadata
            {
                Title = document.Title,
                Tags = document.Tags,
                CreatedAt = document.CreatedAt,
                CustomMetadata = new Dictionary<string, string>
                {
                    ["userId"] = _userId,
                    ["documentType"] = document.DocumentType
                }
            }
        });

        document.IsIndexed = true;
        document.IndexedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<RAGResponse> QueryUserDocumentsAsync(string query)
    {
        // Query with user filter - only retrieves user's documents
        return await _ragEngine.QueryAsync(
            query,
            filter: new MetadataFilter
            {
                MustMatch = new Dictionary<string, string>
                {
                    ["userId"] = _userId
                }
            }
        );
    }
}
```

### Step 5: AI Service with Chat History

```csharp
// YourProject.API/Services/AIService.cs
using Hazina.AI.FluentAPI;
using Hazina.AI.FaultDetection;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public interface IAIService
{
    Task<ChatMessage> SendMessageAsync(Guid sessionId, string message);
    Task<ChatSession> CreateSessionAsync(string title);
    Task<List<ChatSession>> GetUserSessionsAsync();
    Task<ChatSession?> GetSessionWithMessagesAsync(Guid sessionId);
}

public class AIService : IAIService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserDocumentService _documentService;
    private readonly IAdaptiveFaultHandler _faultHandler;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _userId;

    public AIService(
        ApplicationDbContext context,
        IUserDocumentService documentService,
        IAdaptiveFaultHandler faultHandler,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _documentService = documentService;
        _faultHandler = faultHandler;
        _httpContextAccessor = httpContextAccessor;
        _userId = httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
    }

    public async Task<ChatSession> CreateSessionAsync(string title)
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        _context.ChatSessions.Add(session);
        await _context.SaveChangesAsync();

        return session;
    }

    public async Task<ChatMessage> SendMessageAsync(Guid sessionId, string message)
    {
        // 1. Verify session belongs to user
        var session = await _context.ChatSessions
            .Include(s => s.Messages.OrderBy(m => m.CreatedAt).TakeLast(10))
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == _userId)
            ?? throw new NotFoundException("Session not found");

        // 2. Save user message
        var userMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = "user",
            Content = message,
            CreatedAt = DateTime.UtcNow
        };
        _context.ChatMessages.Add(userMessage);

        // 3. Query user's documents for context (RAG)
        var ragResponse = await _documentService.QueryUserDocumentsAsync(message);

        // 4. Build conversation context
        var conversationHistory = session.Messages
            .Select(m => new HazinaChatMessage
            {
                Role = m.Role == "user"
                    ? HazinaMessageRole.User
                    : HazinaMessageRole.Assistant,
                Text = m.Content
            })
            .ToList();

        // 5. Add RAG context as system message
        var systemPrompt = BuildSystemPrompt(ragResponse);

        // 6. Execute with fault detection
        var result = await Hazina.AI()
            .WithFaultDetection(confidence: 0.8)
            .WithSystemMessage(systemPrompt)
            .Ask(message)
            .ExecuteAsync();

        // 7. Save assistant response
        var assistantMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = "assistant",
            Content = result.Response,
            Citations = ragResponse.Citations?.ToArray(),
            Confidence = result.Confidence,
            CreatedAt = DateTime.UtcNow
        };
        _context.ChatMessages.Add(assistantMessage);

        // 8. Update session activity
        session.LastActivityAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return assistantMessage;
    }

    private string BuildSystemPrompt(RAGResponse ragResponse)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a helpful assistant with access to the user's documents.");
        sb.AppendLine();

        if (ragResponse.RetrievedDocuments?.Any() == true)
        {
            sb.AppendLine("RELEVANT CONTEXT FROM USER'S DOCUMENTS:");
            sb.AppendLine("─".PadRight(50, '─'));

            foreach (var doc in ragResponse.RetrievedDocuments)
            {
                sb.AppendLine($"[{doc.Metadata.Title}]");
                sb.AppendLine(doc.Content);
                sb.AppendLine();
            }

            sb.AppendLine("─".PadRight(50, '─'));
            sb.AppendLine();
            sb.AppendLine("Use the above context to answer the user's question.");
            sb.AppendLine("If the context doesn't contain relevant information, say so.");
            sb.AppendLine("Always cite your sources when using information from documents.");
        }
        else
        {
            sb.AppendLine("No relevant documents found. Answer based on general knowledge.");
        }

        return sb.ToString();
    }

    public async Task<List<ChatSession>> GetUserSessionsAsync()
    {
        return await _context.ChatSessions
            .Where(s => s.UserId == _userId)
            .OrderByDescending(s => s.LastActivityAt)
            .ToListAsync();
    }

    public async Task<ChatSession?> GetSessionWithMessagesAsync(Guid sessionId)
    {
        return await _context.ChatSessions
            .Include(s => s.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == _userId);
    }
}
```

### Step 6: API Controllers

```csharp
// YourProject.API/Controllers/ChatController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IAIService _aiService;

    public ChatController(IAIService aiService)
    {
        _aiService = aiService;
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<ChatSession>> CreateSession([FromBody] CreateSessionRequest request)
    {
        var session = await _aiService.CreateSessionAsync(request.Title);
        return CreatedAtAction(nameof(GetSession), new { id = session.Id }, session);
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<List<ChatSession>>> GetSessions()
    {
        return await _aiService.GetUserSessionsAsync();
    }

    [HttpGet("sessions/{id}")]
    public async Task<ActionResult<ChatSession>> GetSession(Guid id)
    {
        var session = await _aiService.GetSessionWithMessagesAsync(id);
        if (session == null) return NotFound();
        return session;
    }

    [HttpPost("sessions/{sessionId}/messages")]
    public async Task<ActionResult<ChatMessage>> SendMessage(
        Guid sessionId,
        [FromBody] SendMessageRequest request)
    {
        try
        {
            var message = await _aiService.SendMessageAsync(sessionId, request.Content);
            return Ok(message);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IUserDocumentService _documentService;

    public DocumentsController(IUserDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpPost]
    public async Task<ActionResult<UserDocument>> UploadDocument(
        [FromBody] UploadDocumentRequest request)
    {
        var document = await _documentService.AddDocumentAsync(
            request.Title,
            request.Content,
            request.Tags
        );
        return CreatedAtAction(nameof(GetDocuments), document);
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDocument>>> GetDocuments()
    {
        return await _documentService.GetUserDocumentsAsync();
    }

    [HttpPost("index")]
    public async Task<IActionResult> IndexDocuments()
    {
        await _documentService.IndexUserDocumentsAsync();
        return Ok(new { message = "Documents indexed successfully" });
    }

    [HttpPost("query")]
    public async Task<ActionResult<RAGResponse>> QueryDocuments(
        [FromBody] QueryRequest request)
    {
        var response = await _documentService.QueryUserDocumentsAsync(request.Query);
        return Ok(response);
    }
}

// Request DTOs
public record CreateSessionRequest(string Title);
public record SendMessageRequest(string Content);
public record UploadDocumentRequest(string Title, string Content, string[] Tags);
public record QueryRequest(string Query);
```

### Step 7: Program.cs Configuration

```csharp
// YourProject.API/Program.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Authentication
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// Hazina AI Services
builder.Services.AddHazinaAI(builder.Configuration);

// HTTP Context accessor (needed for user-scoped services)
builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Step 8: appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=yourapp;Username=postgres;Password=...",
    "Embeddings": "Host=localhost;Database=yourapp_vectors;Username=postgres;Password=..."
  },
  "Hazina": {
    "OpenAI": {
      "ApiKey": "sk-...",
      "Model": "gpt-4o-mini",
      "EmbeddingModel": "text-embedding-3-small"
    },
    "Anthropic": {
      "ApiKey": "sk-ant-...",
      "Model": "claude-3-5-sonnet-20241022"
    },
    "RAG": {
      "ChunkSize": 512,
      "ChunkOverlap": 50,
      "TopK": 5,
      "MinSimilarity": 0.7
    }
  },
  "Jwt": {
    "Issuer": "https://yourapp.com",
    "Audience": "https://yourapp.com",
    "Key": "your-256-bit-secret-key-here-make-it-long"
  }
}
```

---

## Part 4: Production Patterns

### Hallucination Detection

Hazina detects 7 types of hallucinations automatically:

```csharp
var result = await Hazina.AI()
    .WithFaultDetection(confidence: 0.9)
    .WithGroundTruth("company_founded", "2020")
    .WithGroundTruth("ceo_name", "John Smith")
    .Ask("When was our company founded and who is the CEO?")
    .ExecuteAsync();

// Automatically retries if:
// 1. FabricatedFact - Makes up facts not in context
// 2. Contradiction - Contradicts provided ground truth
// 3. ContextMismatch - Doesn't align with RAG context
// 4. UnsupportedClaim - Claims without evidence
// 5. AttributionError - Misattributes sources
// 6. TemporalError - Gets dates/times wrong
// 7. QuantitativeError - Gets numbers wrong

Console.WriteLine($"Confidence: {result.Confidence:P0}");
Console.WriteLine($"Hallucinations detected: {result.HallucinationsDetected}");
Console.WriteLine($"Auto-corrected: {result.WasCorrected}");
```

### Cost Management

```csharp
// Configure budget at startup
var orchestrator = QuickSetup.SetupOpenAI("sk-...");

// Set daily budget
orchestrator.CostTracker.SetBudgetLimit(50m, BudgetPeriod.Daily);

// Get notified at thresholds
orchestrator.CostTracker.OnBudgetWarning += (sender, args) =>
{
    Console.WriteLine($"Budget warning: {args.UsedPercentage:P0} used");
    // Send alert, throttle requests, etc.
};

orchestrator.CostTracker.OnBudgetExceeded += (sender, args) =>
{
    Console.WriteLine("Budget exceeded! Requests will fail.");
    // Switch to cheaper provider, queue requests, etc.
};

// Check current usage
var usage = orchestrator.CostTracker.GetCurrentUsage();
Console.WriteLine($"Today's cost: ${usage.TotalCost:F2}");
Console.WriteLine($"Requests: {usage.RequestCount}");
Console.WriteLine($"Tokens: {usage.TotalTokens}");
```

### Multi-Layer Reasoning (NeuroChain)

For critical decisions requiring high confidence:

```csharp
using Hazina.Neurochain.Core;

var neurochain = new NeuroChainOrchestrator(orchestrator);

// Automatic layer selection based on complexity
var result = await neurochain.ProcessAsync(
    "Analyze this contract for legal risks",
    complexity: TaskComplexity.Auto  // System determines optimal layers
);

// Or force specific layers
var expertResult = await neurochain.ProcessAsync(
    "Critical financial decision requiring expert analysis",
    complexity: TaskComplexity.VeryComplex  // Uses all layers: Fast → Deep → Expert
);

Console.WriteLine($"Answer: {result.Answer}");
Console.WriteLine($"Confidence: {result.Confidence:P0}");
Console.WriteLine($"Layers used: {string.Join(" → ", result.LayersUsed)}");
Console.WriteLine($"Cost: ${result.TotalCost:F4}");
```

### Streaming Responses

```csharp
[HttpPost("sessions/{sessionId}/messages/stream")]
public async Task StreamMessage(
    Guid sessionId,
    [FromBody] SendMessageRequest request)
{
    Response.ContentType = "text/event-stream";

    var fullResponse = await Hazina.AI()
        .WithFaultDetection()
        .Ask(request.Content)
        .ExecuteStreamAsync(async chunk =>
        {
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(new { chunk })}\n\n");
            await Response.Body.FlushAsync();
        });

    // Send final message
    await Response.WriteAsync($"data: {JsonSerializer.Serialize(new { done = true, fullResponse })}\n\n");
}
```

---

## Part 5: Agent Workflows

### Creating Autonomous Agents

```csharp
using Hazina.AI.Agents;

// Create specialized agents
var researchAgent = new Agent(
    name: "Researcher",
    description: "Researches topics using web search and documents",
    orchestrator: orchestrator
);

// Register tools
researchAgent.RegisterTool(new WebSearchTool());
researchAgent.RegisterTool(new DocumentQueryTool(ragEngine));

// Execute autonomous task
var result = await researchAgent.ExecuteAsync(
    "Research the latest developments in quantum computing and summarize key findings"
);

Console.WriteLine(result.Result);
Console.WriteLine($"Tools used: {string.Join(", ", result.ToolsUsed)}");
```

### Multi-Agent Workflows

```csharp
using Hazina.AI.Agents;

var engine = new WorkflowEngine();

// Register agents
engine.RegisterAgent(new Agent("Researcher", "Researches topics", orchestrator));
engine.RegisterAgent(new Agent("Analyst", "Analyzes data", orchestrator));
engine.RegisterAgent(new Agent("Writer", "Writes reports", orchestrator));

// Define workflow
var workflow = new Workflow
{
    Name = "ResearchReport",
    Steps = new List<WorkflowStep>
    {
        new WorkflowStep
        {
            Name = "Research",
            Type = StepType.AgentTask,
            AgentName = "Researcher",
            Task = "Research {topic} thoroughly",
            OutputKey = "research_data"
        },
        new WorkflowStep
        {
            Name = "Analyze",
            Type = StepType.AgentTask,
            AgentName = "Analyst",
            Task = "Analyze this data: {research_data}",
            OutputKey = "analysis"
        },
        new WorkflowStep
        {
            Name = "WriteReport",
            Type = StepType.AgentTask,
            AgentName = "Writer",
            Task = "Write executive summary based on: {analysis}",
            OutputKey = "report"
        }
    }
};

// Execute
var result = await engine.ExecuteWorkflowAsync(
    workflow,
    new Dictionary<string, object> { ["topic"] = "AI in Healthcare" }
);

Console.WriteLine($"Report: {result.FinalContext["report"]}");
```

---

## Part 6: Migration Guide

### From LangChain (Python)

**LangChain:**
```python
from langchain.chat_models import ChatOpenAI
from langchain.chains import RetrievalQA
from langchain.vectorstores import Chroma

llm = ChatOpenAI(model="gpt-4")
vectorstore = Chroma.from_documents(documents, embeddings)
qa = RetrievalQA.from_chain_type(llm, retriever=vectorstore.as_retriever())
result = qa.run("What is the capital of France?")
```

**Hazina equivalent:**
```csharp
QuickSetup.SetupAndConfigure("sk-...");
var ragEngine = new RAGEngine(documentStore, embeddingStore, orchestrator);
await ragEngine.IndexDocumentsAsync(documents);
var result = await ragEngine.QueryAsync("What is the capital of France?");
```

### From Semantic Kernel

**Semantic Kernel:**
```csharp
var kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion("gpt-4", apiKey)
    .Build();

var function = kernel.CreateFunctionFromPrompt("Answer: {{$input}}");
var result = await kernel.InvokeAsync(function, new() { ["input"] = "Hello" });
```

**Hazina equivalent:**
```csharp
QuickSetup.SetupAndConfigure("sk-...");
var result = await Hazina.AskAsync("Hello");
```

### From Direct OpenAI SDK

**OpenAI SDK:**
```csharp
var client = new OpenAIClient(apiKey);
var response = await client.GetChatCompletionsAsync(
    new ChatCompletionsOptions
    {
        DeploymentName = "gpt-4",
        Messages = { new ChatMessage(ChatRole.User, "Hello") }
    });
```

**Hazina equivalent:**
```csharp
QuickSetup.SetupAndConfigure("sk-...");
var result = await Hazina.AskAsync("Hello");
// Plus: automatic failover, cost tracking, hallucination detection
```

---

## Part 7: Rapid Full-Stack Deployment with Lovable

### The Complete Picture: Backend + Frontend in Hours

With Hazina handling your AI backend infrastructure, the remaining challenge is building a frontend. Here's where **Lovable** (https://lovable.dev) comes in—an AI-powered frontend generator that can consume your Swagger/OpenAPI specification and generate a complete, connected React application.

### Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         FULL-STACK AI PLATFORM                            │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                           │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │                      FRONTEND (Lovable-Generated)                    │ │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌────────────┐  │ │
│  │  │   React     │  │  TypeScript │  │  Tailwind   │  │   Shadcn   │  │ │
│  │  │   Components│  │   Types     │  │   CSS       │  │   UI       │  │ │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └────────────┘  │ │
│  │                           │                                          │ │
│  │                  Auto-generated from Swagger                         │ │
│  └─────────────────────────────┬───────────────────────────────────────┘ │
│                                │                                          │
│                         HTTP/REST API                                     │
│                                │                                          │
│  ┌─────────────────────────────▼───────────────────────────────────────┐ │
│  │                      BACKEND (Hazina-Powered)                        │ │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌────────────┐  │ │
│  │  │  ASP.NET    │  │   Swagger   │  │   Hazina    │  │    EF      │  │ │
│  │  │  Core API   │  │   OpenAPI   │  │   AI Engine │  │   Identity │  │ │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └────────────┘  │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│                                                                           │
└──────────────────────────────────────────────────────────────────────────┘
```

### Why Swagger Makes This Easy

Your Hazina backend already includes Swagger/OpenAPI out of the box:

```csharp
// Already in your Program.cs
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ...

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

This generates a complete API specification at `/swagger/v1/swagger.json` describing:
- All endpoints (ChatController, DocumentsController, etc.)
- Request/response schemas (ChatMessage, UserDocument, etc.)
- Authentication requirements
- Parameter types and validation

### Connecting Lovable to Your Backend

**Step 1: Start Your Backend**
```bash
cd YourProject.API
dotnet run
# API running at https://localhost:5001
# Swagger UI at https://localhost:5001/swagger
```

**Step 2: Generate Frontend with Lovable**

1. Go to [Lovable](https://lovable.dev)
2. Describe your app: *"Create a chat application with document management. Users can upload documents, ask questions, and get AI-powered answers with citations."*
3. Provide your Swagger URL: `https://localhost:5001/swagger/v1/swagger.json`
4. Lovable generates:
   - React components for chat, documents, auth
   - TypeScript types matching your DTOs
   - API client with full type safety
   - Responsive UI with Tailwind CSS

**Step 3: Connect and Deploy**

```typescript
// Lovable generates API client like this:
import { Configuration, ChatApi, DocumentsApi } from './generated-api';

const config = new Configuration({
    basePath: 'https://your-api.com',
    accessToken: () => localStorage.getItem('token') || ''
});

export const chatApi = new ChatApi(config);
export const documentsApi = new DocumentsApi(config);

// Usage in components
const response = await chatApi.sendMessage(sessionId, { content: message });
```

### Time to Full-Stack Production

| Component | Tool | Time |
|-----------|------|------|
| AI Infrastructure | Hazina | 30 minutes |
| Backend API | ASP.NET Core + Hazina | 2 hours |
| Database + Auth | EF Core + Identity | 1 hour |
| Frontend | Lovable | 1 hour |
| Integration | Swagger auto-connect | 15 minutes |
| **Total** | | **< 5 hours** |

### Compare to Traditional Development

| Component | Traditional | Time |
|-----------|-------------|------|
| AI Infrastructure | Custom | 3+ months |
| Backend API | Manual coding | 2 weeks |
| Database + Auth | Manual setup | 1 week |
| Frontend | Manual React | 4+ weeks |
| Integration | Manual API calls | 1 week |
| **Total** | | **8+ weeks** |

### Best Practices for Lovable Integration

1. **Rich Swagger Documentation**
   ```csharp
   // Add XML comments to your controllers
   /// <summary>
   /// Sends a message to the AI and gets a response
   /// </summary>
   /// <param name="sessionId">The chat session ID</param>
   /// <param name="request">The message content</param>
   /// <returns>The AI's response with citations</returns>
   [HttpPost("sessions/{sessionId}/messages")]
   public async Task<ActionResult<ChatMessage>> SendMessage(...)
   ```

2. **Enable CORS for Development**
   ```csharp
   builder.Services.AddCors(options =>
   {
       options.AddPolicy("Development", policy =>
       {
           policy.WithOrigins("http://localhost:3000") // Lovable dev server
                 .AllowAnyHeader()
                 .AllowAnyMethod()
                 .AllowCredentials();
       });
   });
   ```

3. **Expose Swagger in Production** (for API documentation)
   ```csharp
   // Always enable Swagger for API consumers
   app.UseSwagger();
   if (app.Environment.IsDevelopment())
   {
       app.UseSwaggerUI();
   }
   ```

### The Complete Stack

```
┌─────────────────────────────────────────────────────────────┐
│                    YOUR PRODUCTION PLATFORM                  │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│   Lovable Frontend          →    User Interface              │
│        │                                                     │
│        │ (Swagger-generated API client)                     │
│        ▼                                                     │
│   ASP.NET Core + Swagger    →    REST API                   │
│        │                                                     │
│        │ (Dependency Injection)                              │
│        ▼                                                     │
│   Hazina AI Services        →    AI Processing              │
│        │                         - Multi-provider           │
│        │                         - RAG pipeline             │
│        │                         - Hallucination detection  │
│        ▼                         - Cost management          │
│   EF Core + Identity        →    Data & Security            │
│        │                         - User management          │
│        │                         - Document storage         │
│        ▼                         - Chat history             │
│   PostgreSQL + pgvector     →    Persistence                │
│                                  - Relational data          │
│                                  - Vector embeddings        │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### Summary

The combination of **Hazina** (backend AI) + **Lovable** (frontend generation) + **Swagger** (API contract) enables you to go from idea to production full-stack AI application in **hours, not months**.

- **Hazina** eliminates AI infrastructure complexity
- **Swagger** provides the contract between backend and frontend
- **Lovable** generates a complete, type-safe frontend from that contract

This is the fastest path to deploying a production RAG-first AI platform.

---

## Conclusion

### What You Get with Hazina

| Capability | Lines of Code | Time to Implement |
|------------|---------------|-------------------|
| Multi-provider with failover | 4 | 5 minutes |
| RAG pipeline | 20 | 30 minutes |
| Hallucination detection | 0 (built-in) | 0 |
| Cost tracking | 0 (built-in) | 0 |
| EF Identity integration | 100 | 2 hours |
| Agent workflows | 50 | 1 hour |
| **Total production system** | **~200** | **< 1 day** |

### Compare to Building from Scratch

| Capability | Traditional | Time |
|------------|-------------|------|
| Multi-provider abstraction | 500+ lines | 2 weeks |
| Failover logic | 300+ lines | 1 week |
| RAG pipeline | 1000+ lines | 3 weeks |
| Hallucination detection | 800+ lines | 4 weeks |
| Cost tracking | 400+ lines | 1 week |
| Testing & hardening | 2000+ lines | 4 weeks |
| **Total** | **5000+ lines** | **15+ weeks** |

### The Bottom Line

Hazina reduces AI infrastructure development from **15+ weeks to less than 1 day** while providing:

- Production-grade reliability
- Automatic provider failover
- Built-in hallucination detection
- Real-time cost management
- Full audit trails
- Enterprise security integration

**Start building your RAG-first AI platform today.**

---

## Resources

- [Hazina GitHub Repository](https://github.com/hazina/hazina)
- [API Documentation](./docs/API.md)
- [Agent Workflows Guide](./docs/AGENTS_GUIDE.md)
- [NeuroChain Guide](./docs/NEUROCHAIN_GUIDE.md)
- [Production Monitoring Guide](./docs/PRODUCTION_MONITORING_GUIDE.md)

---

*This guide is part of the Hazina documentation. For questions or contributions, please open an issue on GitHub.*
