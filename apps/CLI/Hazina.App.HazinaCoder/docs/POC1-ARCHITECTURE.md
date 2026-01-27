# POC 1: Persistent Learning System - Architecture

**Created:** 2026-01-26
**Status:** 🏗️ DESIGN PHASE
**Goal:** User says "I prefer X" → HazinaCoder remembers forever
**Success Metric:** 95% retention across sessions

---

## 🎯 OBJECTIVE

Build the simplest possible system that demonstrates genuine persistent learning - the foundation for all cognitive AI features in v2.0.

**Core Capability:**
When a user expresses a preference, pattern, or solution, HazinaCoder stores it permanently and retrieves it automatically in future sessions without being reminded.

---

## 🏗️ SYSTEM ARCHITECTURE

```
┌─────────────────────────────────────────────────────────────┐
│                     HazinaCoder CLI                          │
│                                                              │
│  ┌────────────────────────────────────────────────────┐   │
│  │         Experience Capture Layer                    │   │
│  │  - Detect user preferences                          │   │
│  │  - Extract patterns from interactions               │   │
│  │  - Tag and categorize experiences                   │   │
│  └────────────────┬───────────────────────────────────┘   │
│                   │                                          │
│                   ↓                                          │
│  ┌────────────────────────────────────────────────────┐   │
│  │         Experience Storage Layer                    │   │
│  │  - Generate embeddings (OpenAI API)                 │   │
│  │  - Store vectors in Qdrant                          │   │
│  │  - Store metadata in JSON payload                   │   │
│  └────────────────┬───────────────────────────────────┘   │
│                   │                                          │
│                   ↓                                          │
│  ┌────────────────────────────────────────────────────┐   │
│  │            Qdrant Vector Database                   │   │
│  │  Collection: "hazinacoder_experiences"              │   │
│  │  Vectors: 1536-dim (OpenAI embeddings)              │   │
│  │  Distance: Cosine similarity                        │   │
│  └────────────────┬───────────────────────────────────┘   │
│                   │                                          │
│                   ↑                                          │
│  ┌────────────────────────────────────────────────────┐   │
│  │         Experience Retrieval Layer                  │   │
│  │  - Generate query embeddings                        │   │
│  │  - Similarity search in Qdrant                      │   │
│  │  - Rank and filter results                          │   │
│  │  - Return top-K relevant experiences                │   │
│  └────────────────┬───────────────────────────────────┘   │
│                   │                                          │
│                   ↓                                          │
│  ┌────────────────────────────────────────────────────┐   │
│  │         Application Layer                           │   │
│  │  - Apply learned preferences automatically          │   │
│  │  - Suggest solutions based on past success          │   │
│  │  - Explain why preference was applied               │   │
│  └────────────────────────────────────────────────────┘   │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 📊 DATA MODEL

### Experience

```csharp
public class Experience
{
    // Unique identifier
    public Guid Id { get; set; }

    // When this experience occurred
    public DateTime Timestamp { get; set; }

    // Type of experience
    public ExperienceType Type { get; set; }

    // Context in which this happened
    public ExperienceContext Context { get; set; }

    // The actual content (preference, pattern, solution)
    public string Content { get; set; }

    // Natural language description for retrieval
    public string Description { get; set; }

    // Embedding vector (generated from description + content)
    public float[] Embedding { get; set; } // 1536-dim

    // Metadata for filtering and ranking
    public ExperienceMetadata Metadata { get; set; }

    // Tags for categorization
    public List<string> Tags { get; set; }

    // Success/failure outcome (if applicable)
    public ExperienceOutcome? Outcome { get; set; }
}

public enum ExperienceType
{
    UserPreference,    // "I prefer async/await"
    CodePattern,       // Successful code solution
    ErrorResolution,   // How an error was fixed
    ProjectContext,    // Architecture decisions
    UserInsight,       // Understanding about the user
    ToolUsage,         // Effective tool combinations
    WorkflowPattern    // Effective workflow sequences
}

public class ExperienceContext
{
    public string ProjectName { get; set; }
    public string FileName { get; set; }
    public string FunctionName { get; set; }
    public string Language { get; set; }
    public string Framework { get; set; }
    public WorkflowMode Mode { get; set; }
    public List<Goal> ActiveGoals { get; set; }
}

public class ExperienceMetadata
{
    // Importance score (0.0 - 1.0)
    public double Importance { get; set; }

    // Emotional intensity (0.0 - 1.0)
    public double EmotionalIntensity { get; set; }

    // Novelty (0.0 - 1.0, decreases with similar experiences)
    public double Novelty { get; set; }

    // How many times retrieved (popularity)
    public int RetrievalCount { get; set; }

    // Last retrieved timestamp
    public DateTime? LastRetrieved { get; set; }

    // Confidence in this experience (0.0 - 1.0)
    public double Confidence { get; set; }
}

public class ExperienceOutcome
{
    public bool Successful { get; set; }
    public string Result { get; set; }
    public double Satisfaction { get; set; } // User satisfaction (0.0 - 1.0)
}
```

### Qdrant Collection Schema

```yaml
collection_name: "hazinacoder_experiences"

vector_config:
  size: 1536
  distance: Cosine

payload_schema:
  id: uuid
  timestamp: datetime
  type: string
  context:
    project_name: string
    file_name: string
    function_name: string
    language: string
    framework: string
    mode: string
  content: string
  description: string
  tags: array[string]
  metadata:
    importance: float
    emotional_intensity: float
    novelty: float
    retrieval_count: integer
    last_retrieved: datetime
    confidence: float
  outcome:
    successful: boolean
    result: string
    satisfaction: float

indexes:
  - timestamp (for time-based queries)
  - type (for filtering by experience type)
  - tags (for category filtering)
  - metadata.importance (for ranking)
```

---

## 🔧 IMPLEMENTATION COMPONENTS

### 1. Experience Capture System

**File:** `Core/Learning/ExperienceCapture.cs`

```csharp
public class ExperienceCapture
{
    private readonly OpenAIClient _openai;
    private readonly ExperienceStorage _storage;

    // Automatically detect and capture experiences
    public async Task CaptureFromInteraction(UserInteraction interaction)
    {
        // Detect if this is a preference statement
        if (IsPreferenceStatement(interaction.Message))
        {
            await CapturePreference(interaction);
        }

        // Detect if this is a successful solution
        if (IsSuccessfulSolution(interaction))
        {
            await CapturePattern(interaction);
        }

        // Detect if this is an error resolution
        if (IsErrorResolution(interaction))
        {
            await CaptureResolution(interaction);
        }
    }

    private bool IsPreferenceStatement(string message)
    {
        // Patterns: "I prefer...", "I like...", "Use...", "Don't use..."
        var patterns = new[]
        {
            @"I prefer (?<preference>.+)",
            @"I like (?<preference>.+)",
            @"always use (?<preference>.+)",
            @"never use (?<preference>.+)",
            @"use (?<preference>.+) instead of"
        };

        return patterns.Any(p => Regex.IsMatch(message, p, RegexOptions.IgnoreCase));
    }

    private async Task CapturePreference(UserInteraction interaction)
    {
        var experience = new Experience
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Type = ExperienceType.UserPreference,
            Context = GetCurrentContext(),
            Content = ExtractPreference(interaction.Message),
            Description = $"User prefers: {ExtractPreference(interaction.Message)}",
            Tags = new List<string> { "preference", "user-stated" },
            Metadata = new ExperienceMetadata
            {
                Importance = 0.9, // Explicit preferences are important
                EmotionalIntensity = 0.5,
                Novelty = CalculateNovelty(interaction),
                Confidence = 1.0 // User explicitly stated
            }
        };

        await _storage.StoreExperience(experience);
    }
}
```

### 2. Experience Storage System

**File:** `Core/Learning/ExperienceStorage.cs`

```csharp
public class ExperienceStorage
{
    private readonly QdrantClient _qdrant;
    private readonly OpenAIClient _openai;
    private const string COLLECTION_NAME = "hazinacoder_experiences";

    public async Task StoreExperience(Experience experience)
    {
        // Generate embedding for description + content
        var textToEmbed = $"{experience.Description}\n{experience.Content}";
        var embedding = await GenerateEmbedding(textToEmbed);

        experience.Embedding = embedding;

        // Store in Qdrant
        await _qdrant.Upsert(
            collectionName: COLLECTION_NAME,
            points: new[]
            {
                new PointStruct
                {
                    Id = experience.Id,
                    Vectors = embedding,
                    Payload = new Dictionary<string, object>
                    {
                        ["timestamp"] = experience.Timestamp,
                        ["type"] = experience.Type.ToString(),
                        ["context"] = experience.Context,
                        ["content"] = experience.Content,
                        ["description"] = experience.Description,
                        ["tags"] = experience.Tags,
                        ["metadata"] = experience.Metadata,
                        ["outcome"] = experience.Outcome
                    }
                }
            }
        );
    }

    private async Task<float[]> GenerateEmbedding(string text)
    {
        var response = await _openai.GetEmbeddingsAsync(
            new EmbeddingsOptions("text-embedding-3-small", new[] { text })
        );

        return response.Value.Data[0].Embedding.ToArray();
    }

    public async Task InitializeCollection()
    {
        // Create collection if doesn't exist
        var collections = await _qdrant.ListCollectionsAsync();
        if (!collections.Any(c => c.Name == COLLECTION_NAME))
        {
            await _qdrant.CreateCollectionAsync(
                collectionName: COLLECTION_NAME,
                vectorsConfig: new VectorParams
                {
                    Size = 1536,
                    Distance = Distance.Cosine
                }
            );
        }
    }
}
```

### 3. Experience Retrieval System

**File:** `Core/Learning/ExperienceRetrieval.cs`

```csharp
public class ExperienceRetrieval
{
    private readonly QdrantClient _qdrant;
    private readonly OpenAIClient _openai;
    private const string COLLECTION_NAME = "hazinacoder_experiences";

    public async Task<List<Experience>> FindSimilarExperiences(
        string query,
        int topK = 5,
        ExperienceType? filterType = null)
    {
        // Generate query embedding
        var queryEmbedding = await GenerateEmbedding(query);

        // Build filter
        Filter filter = null;
        if (filterType.HasValue)
        {
            filter = new Filter
            {
                Must = new[]
                {
                    new Condition
                    {
                        Field = "type",
                        Match = new Match { Value = filterType.Value.ToString() }
                    }
                }
            };
        }

        // Search Qdrant
        var results = await _qdrant.SearchAsync(
            collectionName: COLLECTION_NAME,
            vector: queryEmbedding,
            limit: (ulong)topK,
            filter: filter,
            scoreThreshold: 0.7 // Only return if similarity > 0.7
        );

        // Convert to Experience objects
        var experiences = results.Select(r => new Experience
        {
            Id = (Guid)r.Id,
            Timestamp = (DateTime)r.Payload["timestamp"],
            Type = Enum.Parse<ExperienceType>((string)r.Payload["type"]),
            Content = (string)r.Payload["content"],
            Description = (string)r.Payload["description"],
            // ... map other fields
            Metadata = MapMetadata(r.Payload["metadata"])
        }).ToList();

        // Update retrieval count
        foreach (var exp in experiences)
        {
            exp.Metadata.RetrievalCount++;
            exp.Metadata.LastRetrieved = DateTime.UtcNow;
            await UpdateMetadata(exp);
        }

        return experiences;
    }

    public async Task<List<Experience>> GetUserPreferences(string context)
    {
        return await FindSimilarExperiences(
            query: context,
            topK: 10,
            filterType: ExperienceType.UserPreference
        );
    }

    private async Task<float[]> GenerateEmbedding(string text)
    {
        var response = await _openai.GetEmbeddingsAsync(
            new EmbeddingsOptions("text-embedding-3-small", new[] { text })
        );

        return response.Value.Data[0].Embedding.ToArray();
    }
}
```

### 4. Learning System Integration

**File:** `Core/Learning/LearningSystem.cs`

```csharp
public class LearningSystem
{
    private readonly ExperienceCapture _capture;
    private readonly ExperienceStorage _storage;
    private readonly ExperienceRetrieval _retrieval;

    public async Task ProcessInteraction(UserInteraction interaction)
    {
        // Capture experience from interaction
        await _capture.CaptureFromInteraction(interaction);
    }

    public async Task<List<Experience>> RecallRelevantExperiences(string context)
    {
        // Retrieve relevant past experiences
        return await _retrieval.FindSimilarExperiences(context);
    }

    public async Task<string> ApplyLearnedPreferences(string task)
    {
        // Get relevant preferences
        var preferences = await _retrieval.GetUserPreferences(task);

        if (preferences.Any())
        {
            var explanation = "Based on your preferences:\n";
            foreach (var pref in preferences.Take(3))
            {
                explanation += $"- {pref.Description} (learned {GetTimeAgo(pref.Timestamp)})\n";
            }

            return explanation;
        }

        return null;
    }

    private string GetTimeAgo(DateTime timestamp)
    {
        var span = DateTime.UtcNow - timestamp;
        if (span.TotalDays > 30) return $"{(int)(span.TotalDays / 30)} months ago";
        if (span.TotalDays > 1) return $"{(int)span.TotalDays} days ago";
        if (span.TotalHours > 1) return $"{(int)span.TotalHours} hours ago";
        return "recently";
    }
}
```

---

## 🔌 INTEGRATION WITH HAZINACODER

### Program.cs Integration

```csharp
public class HazinaCoderCLI
{
    private AgentIdentity? _identity;
    private LearningSystem? _learning;

    private async Task InitializeLearningSystem()
    {
        // Set up Qdrant client
        var qdrantClient = new QdrantClient("localhost", 6334);

        // Set up OpenAI client (for embeddings)
        var openaiClient = new OpenAIClient(
            Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        );

        // Initialize learning components
        var storage = new ExperienceStorage(qdrantClient, openaiClient);
        await storage.InitializeCollection();

        var capture = new ExperienceCapture(openaiClient, storage);
        var retrieval = new ExperienceRetrieval(qdrantClient, openaiClient);

        _learning = new LearningSystem(capture, storage, retrieval);
    }

    private async Task ProcessUserMessage(string message)
    {
        // Create interaction object
        var interaction = new UserInteraction
        {
            Message = message,
            Timestamp = DateTime.UtcNow,
            Context = GetCurrentContext()
        };

        // Capture learning from interaction
        await _learning.ProcessInteraction(interaction);

        // Recall relevant experiences
        var relevantExperiences = await _learning.RecallRelevantExperiences(message);

        // Apply learned preferences
        var preferences = await _learning.ApplyLearnedPreferences(message);
        if (preferences != null)
        {
            AnsiConsole.MarkupLine($"[dim]{preferences}[/]");
        }

        // Continue with normal processing...
    }
}
```

---

## 📦 DEPENDENCIES

### NuGet Packages

```xml
<ItemGroup>
  <!-- Qdrant vector database client -->
  <PackageReference Include="Qdrant.Client" Version="1.11.0" />

  <!-- OpenAI for embeddings -->
  <PackageReference Include="Azure.AI.OpenAI" Version="1.0.0-beta.17" />

  <!-- Existing dependencies -->
  <PackageReference Include="YamlDotNet" Version="16.2.1" />
  <PackageReference Include="Spectre.Console" Version="0.49.1" />
</ItemGroup>
```

### External Services

```yaml
Qdrant:
  Installation: Docker or standalone binary
  Command: docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant
  Storage: ./qdrant_storage (persistent)

OpenAI:
  API: text-embedding-3-small model
  Cost: $0.00002 per 1K tokens
  Rate Limit: 5000 requests/minute

Environment Variables:
  OPENAI_API_KEY: Required for embeddings
```

---

## 🎯 SUCCESS CRITERIA

**Functional Requirements:**
- ✅ User states preference → stored in <1 second
- ✅ Preference retrieved automatically in future sessions
- ✅ Retrieval accuracy >80% for top-5 results
- ✅ Response time <500ms for retrieval
- ✅ No data loss across restarts

**Quality Requirements:**
- ✅ 95%+ retention rate (preferences not forgotten)
- ✅ No false positives (irrelevant preferences not applied)
- ✅ Graceful degradation (works without internet for local operations)
- ✅ Clear explanations (user understands why preference was applied)

**Testing Requirements:**
- ✅ 20+ test preferences with varied contexts
- ✅ Multi-session testing (restart between tests)
- ✅ Performance benchmarks (embedding generation, retrieval time)
- ✅ Edge cases (conflicting preferences, ambiguous statements)

---

## 📈 METRICS TO TRACK

```csharp
public class LearningMetrics
{
    // Storage metrics
    public int TotalExperiencesStored { get; set; }
    public int ExperiencesByType { get; set; }
    public long StorageSizeBytes { get; set; }

    // Retrieval metrics
    public int TotalRetrievals { get; set; }
    public double AverageRetrievalTimeMs { get; set; }
    public double AverageRelevanceScore { get; set; }

    // Quality metrics
    public double RetentionRate { get; set; } // % of preferences remembered
    public double PrecisionAtK { get; set; } // Accuracy of top-K results
    public double UserSatisfaction { get; set; } // Inferred from outcomes

    // Cost metrics
    public int EmbeddingAPICallsCount { get; set; }
    public double TotalEmbeddingCost { get; set; }
}
```

---

## 🚧 KNOWN LIMITATIONS & FUTURE WORK

**Current Limitations:**
- **No consolidation:** All experiences stored, no pattern extraction yet
- **Simple embeddings:** Using OpenAI embeddings, not fine-tuned
- **No prioritization:** All experiences weighted equally
- **No forgetting:** No mechanism to forget outdated preferences
- **Single-user:** No multi-user isolation

**Future Enhancements (Phase 2.2+):**
- Offline consolidation (memory replay during idle)
- Pattern extraction (semantic memory)
- Importance-based prioritization
- Time-decay for outdated preferences
- Fine-tuned embeddings on coding domain
- Multi-user support with isolation
- Conflict resolution (handle contradictory preferences)

---

## 🎓 RESEARCH FOUNDATION

This POC implements concepts from:
- **Complementary Learning Systems** (McClelland) - Fast episodic storage
- **Memory Reactivation** (Wilson) - Foundation for future consolidation
- **Semantic Memory** - Vector similarity for retrieval

**NOT YET IMPLEMENTED:**
- Slow semantic consolidation (future)
- Offline replay (future)
- Catastrophic forgetting prevention (future)

---

**Status:** 📐 ARCHITECTURE COMPLETE - Ready for Implementation
**Next:** Task 2 - Set up Qdrant vector database
**Timeline:** 2-3 days for full POC
**Expected Outcome:** Working persistent learning system

---

**Created:** 2026-01-26
**Author:** HazinaCoder (Autonomous Decision)
**Phase:** POC 1 - Persistent Learning
