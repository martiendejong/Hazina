# Hazina Generic Content Framework - Design & Implementation Plan

**Date:** 2026-01-19
**Purpose:** Extend Hazina with generic content import, calendar, and AI inspiration infrastructure
**Target:** Make content management platform-agnostic and reusable across all applications

---

## 🎯 VISION

**Current State:**
- ✅ Social import abstractions (ISocialProvider, ISocialContentStore)
- ✅ WordPress provider implementation
- ✅ Document Store with RAG capabilities
- ❌ Content model too limited (separate Posts/Articles)
- ❌ No content analysis services
- ❌ No calendar abstraction
- ❌ No AI inspiration engine

**Target State:**
- ✅ **Unified Content Model** - One model for all sources (WordPress, LinkedIn, Instagram, etc.)
- ✅ **Content Analysis Pipeline** - Automatic style/topic/sentiment analysis
- ✅ **Calendar Event Abstraction** - Generic calendar for any content type
- ✅ **AI Inspiration Engine** - Brand voice learning and content generation
- ✅ **LLM-Friendly Storage** - Optimized for AI tool queries

---

## 📁 WHAT GOES WHERE?

### **HAZINA (Framework) - Generic Infrastructure**

```
Hazina.Tools.Services.Social/
├── Abstractions/
│   ├── ISocialProvider.cs ✅ EXISTS
│   ├── ISocialContentStore.cs ✅ EXISTS (needs extension)
│   ├── IUnifiedContentStore.cs ⭐ NEW - Replaces split Posts/Articles
│   ├── IContentAnalyzer.cs ⭐ NEW - Style/topic/sentiment analysis
│   ├── IContentCalendarService.cs ⭐ NEW - Calendar abstraction
│   └── IContentInspirationEngine.cs ⭐ NEW - AI-powered inspiration
│
├── Models/
│   ├── UnifiedContent.cs ⭐ NEW - Replaces SocialPost/SocialArticle
│   ├── ContentComment.cs ⭐ NEW - Generic comment model
│   ├── ContentMedia.cs ⭐ NEW - Generic media model
│   ├── ContentAnalysis.cs ⭐ NEW - Analysis results
│   ├── WritingProfile.cs ⭐ NEW - Brand voice profile
│   ├── CalendarEvent.cs ⭐ NEW - Generic calendar event
│   └── InspirationContext.cs ⭐ NEW - AI inspiration context
│
├── Services/
│   ├── UnifiedContentStore.cs ⭐ NEW - SQLite/Postgres implementation
│   ├── ContentAnalyzer.cs ⭐ NEW - LLM-powered analysis
│   ├── ContentCalendarService.cs ⭐ NEW - Calendar management
│   └── ContentInspirationEngine.cs ⭐ NEW - AI inspiration
│
└── Providers/
    ├── WordPressProvider.cs ✅ EXISTS (update to use UnifiedContent)
    ├── LinkedInProvider.cs ✅ EXISTS (update)
    └── ... other providers ...
```

### **CLIENT-MANAGER (Application) - UI & Configuration**

```
ClientManagerAPI/
├── Controllers/
│   ├── ContentController.cs ⭐ NEW - Generic content CRUD
│   ├── CalendarController.cs ⭐ NEW - Calendar API
│   └── InspirationController.cs ⭐ NEW - AI inspiration API
│
└── Configuration/
    └── HazinaContentSetup.cs ⭐ NEW - DI configuration

ClientManagerFrontend/
├── components/
│   ├── calendar/ ⭐ NEW - Calendar UI
│   ├── content/ ⭐ NEW - Content library UI
│   └── inspiration/ ⭐ NEW - Inspiration browser UI
│
└── services/
    ├── contentService.ts ⭐ NEW - Generic content API
    └── wordpress.ts ✅ EXISTS (uses contentService)
```

---

## 🏗️ PHASE 1: UNIFIED CONTENT MODEL (Foundation)

### **1.1 UnifiedContent Model**

**File:** `Hazina.Tools.Services.Social/Models/UnifiedContent.cs`

```csharp
namespace Hazina.Tools.Services.Social.Models;

/// <summary>
/// Unified content model that works for ALL sources:
/// WordPress posts/pages, LinkedIn posts, Facebook posts, Instagram posts, etc.
/// </summary>
public class UnifiedContent
{
    // ===== Identity =====
    /// <summary>
    /// Unique ID: {source}-{sourceId} e.g., "wordpress-123", "linkedin-789"
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Project this content belongs to
    /// </summary>
    public string ProjectId { get; set; } = "";

    /// <summary>
    /// Connected account that imported this
    /// </summary>
    public string AccountId { get; set; } = "";

    // ===== Source Information =====
    /// <summary>
    /// Source platform: "wordpress", "linkedin", "facebook", "instagram", "twitter"
    /// </summary>
    public string SourceType { get; set; } = "";

    /// <summary>
    /// Original ID from source platform
    /// </summary>
    public string SourceId { get; set; } = "";

    /// <summary>
    /// Original URL on source platform
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Content type: "post", "page", "product", "story", "reel", "video", "article"
    /// </summary>
    public string ContentType { get; set; } = "";

    // ===== Core Content =====
    /// <summary>
    /// Title (may be null for posts without titles, e.g., Instagram captions)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Main content/body text
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Summary/excerpt (auto-generated if not provided)
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// HTML version (for rich text platforms)
    /// </summary>
    public string? ContentHtml { get; set; }

    /// <summary>
    /// Plain text version (for search/analysis)
    /// </summary>
    public string? ContentPlainText { get; set; }

    // ===== Media =====
    /// <summary>
    /// Featured/main image URL
    /// </summary>
    public string? FeaturedImageUrl { get; set; }

    /// <summary>
    /// Additional media (images, videos)
    /// </summary>
    public List<ContentMedia> Media { get; set; } = new();

    // ===== Author =====
    /// <summary>
    /// Author display name
    /// </summary>
    public string? AuthorName { get; set; }

    /// <summary>
    /// Author ID on source platform
    /// </summary>
    public string? AuthorId { get; set; }

    // ===== Timestamps =====
    /// <summary>
    /// When content was originally published
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// When content was last modified (may be null)
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// When we imported this content
    /// </summary>
    public DateTime ImportedAt { get; set; }

    /// <summary>
    /// Last time we synced/refreshed this content
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    // ===== Engagement Metrics =====
    /// <summary>
    /// Number of likes/reactions
    /// </summary>
    public int LikeCount { get; set; }

    /// <summary>
    /// Number of comments
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Number of shares/retweets
    /// </summary>
    public int ShareCount { get; set; }

    /// <summary>
    /// Number of views (if available)
    /// </summary>
    public int ViewCount { get; set; }

    /// <summary>
    /// Calculated engagement rate
    /// </summary>
    public double EngagementRate { get; set; }

    // ===== Comments =====
    /// <summary>
    /// Comments on this content
    /// </summary>
    public List<ContentComment> Comments { get; set; } = new();

    // ===== Categorization =====
    /// <summary>
    /// Tags/hashtags
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Categories (WordPress categories, topics, etc.)
    /// </summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>
    /// Language code (e.g., "en", "nl", "de")
    /// </summary>
    public string? Language { get; set; }

    // ===== AI Analysis (Filled after import) =====
    /// <summary>
    /// AI-detected topics
    /// </summary>
    public List<string> DetectedTopics { get; set; } = new();

    /// <summary>
    /// Sentiment: "positive", "neutral", "negative"
    /// </summary>
    public string? Sentiment { get; set; }

    /// <summary>
    /// Tone: "professional", "casual", "technical", "friendly"
    /// </summary>
    public string? Tone { get; set; }

    /// <summary>
    /// Full analysis result
    /// </summary>
    public ContentAnalysis? Analysis { get; set; }

    // ===== Status & Flags =====
    /// <summary>
    /// Status: "published", "draft", "archived", "deleted"
    /// </summary>
    public string Status { get; set; } = "published";

    /// <summary>
    /// Is this historical (imported past content) or future (scheduled)?
    /// </summary>
    public bool IsHistorical { get; set; } = true;

    /// <summary>
    /// Should this appear on calendar?
    /// </summary>
    public bool DisplayOnCalendar { get; set; } = true;

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsDeleted { get; set; }

    // ===== Document Store Integration =====
    /// <summary>
    /// Document store ID for RAG/semantic search
    /// </summary>
    public string? DocumentStoreId { get; set; }

    /// <summary>
    /// Vector embedding ID
    /// </summary>
    public string? EmbeddingId { get; set; }

    // ===== Platform-Specific Metadata =====
    /// <summary>
    /// Platform-specific fields (JSON serialized)
    /// </summary>
    public Dictionary<string, object> PlatformMetadata { get; set; } = new();
}
```

### **1.2 ContentMedia Model**

```csharp
/// <summary>
/// Represents media attached to content (images, videos, documents)
/// </summary>
public class ContentMedia
{
    public string Id { get; set; } = "";
    public string ContentId { get; set; } = "";
    public MediaType Type { get; set; }
    public string Url { get; set; } = "";
    public string? LocalUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? DurationSeconds { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? MimeType { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsFeatured { get; set; }
    public string? Caption { get; set; }
    public string? AltText { get; set; }
    public DateTime ImportedAt { get; set; }
}

public enum MediaType
{
    Image,
    Video,
    Document,
    Audio
}
```

### **1.3 ContentComment Model**

```csharp
/// <summary>
/// Represents a comment on content (works for all platforms)
/// </summary>
public class ContentComment
{
    public string Id { get; set; } = "";
    public string ContentId { get; set; } = "";
    public string? ParentCommentId { get; set; } // For threaded comments
    public string SourceType { get; set; } = ""; // wordpress, linkedin, etc.
    public string SourceCommentId { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string? AuthorId { get; set; }
    public string? AuthorAvatarUrl { get; set; }
    public string? AuthorProfileUrl { get; set; }
    public string CommentText { get; set; } = "";
    public string? CommentHtml { get; set; }
    public int LikeCount { get; set; }
    public int ReplyCount { get; set; }
    public DateTime PostedAt { get; set; }
    public DateTime ImportedAt { get; set; }
    public bool IsDeleted { get; set; }

    // AI Analysis
    public string? Sentiment { get; set; }
    public bool IsQuestion { get; set; }
    public bool RequiresResponse { get; set; }
}
```

---

## 🔄 PHASE 2: UNIFIED CONTENT STORE INTERFACE

### **2.1 IUnifiedContentStore**

**File:** `Hazina.Tools.Services.Social/Abstractions/IUnifiedContentStore.cs`

```csharp
namespace Hazina.Tools.Services.Social.Abstractions;

/// <summary>
/// Generic content storage that works for ALL platforms.
/// Replaces separate ISocialContentStore with Posts/Articles split.
/// </summary>
public interface IUnifiedContentStore
{
    // ===== Basic CRUD =====

    /// <summary>
    /// Saves content items (works for any platform)
    /// </summary>
    Task SaveContentAsync(
        string projectId,
        IEnumerable<UnifiedContent> content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single content item by ID
    /// </summary>
    Task<UnifiedContent?> GetContentAsync(
        string projectId,
        string contentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content items with filtering
    /// </summary>
    Task<List<UnifiedContent>> GetContentListAsync(
        string projectId,
        UnifiedContentQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes content for an account
    /// </summary>
    Task DeleteAccountContentAsync(
        string projectId,
        string accountId,
        CancellationToken cancellationToken = default);

    // ===== Search & Query =====

    /// <summary>
    /// Full-text search across content
    /// </summary>
    Task<List<UnifiedContent>> SearchAsync(
        string projectId,
        string query,
        UnifiedContentSearchOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Semantic search using vector embeddings
    /// </summary>
    Task<List<UnifiedContent>> SemanticSearchAsync(
        string projectId,
        string query,
        UnifiedContentSearchOptions options,
        CancellationToken cancellationToken = default);

    // ===== Calendar Integration =====

    /// <summary>
    /// Gets content items for calendar display in date range
    /// </summary>
    Task<List<UnifiedContent>> GetCalendarEventsAsync(
        string projectId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    // ===== Analytics =====

    /// <summary>
    /// Gets content statistics
    /// </summary>
    Task<UnifiedContentStats> GetStatsAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets top performing content
    /// </summary>
    Task<List<UnifiedContent>> GetTopPerformingAsync(
        string projectId,
        int limit = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Query parameters for content retrieval
/// </summary>
public class UnifiedContentQuery
{
    public string? AccountId { get; set; }
    public List<string>? SourceTypes { get; set; } // Filter by platform
    public List<string>? ContentTypes { get; set; } // Filter by type (post, page, etc.)
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
    public int Limit { get; set; } = 100;
    public int Offset { get; set; } = 0;
    public string? OrderBy { get; set; } = "published_at";
    public bool Descending { get; set; } = true;
}

/// <summary>
/// Search options
/// </summary>
public class UnifiedContentSearchOptions
{
    public List<string>? SourceTypes { get; set; }
    public List<string>? ContentTypes { get; set; }
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
    public int Limit { get; set; } = 20;
    public bool UseSemanticSearch { get; set; } = true;
}

/// <summary>
/// Content statistics
/// </summary>
public class UnifiedContentStats
{
    public int TotalItems { get; set; }
    public Dictionary<string, int> ItemsBySource { get; set; } = new();
    public Dictionary<string, int> ItemsByType { get; set; } = new();
    public int TotalComments { get; set; }
    public long TotalLikes { get; set; }
    public long TotalShares { get; set; }
    public long TotalViews { get; set; }
    public DateTime? OldestContent { get; set; }
    public DateTime? NewestContent { get; set; }
}
```

---

## 🧠 PHASE 3: CONTENT ANALYSIS SERVICE

### **3.1 IContentAnalyzer Interface**

**File:** `Hazina.Tools.Services.Social/Abstractions/IContentAnalyzer.cs`

```csharp
namespace Hazina.Tools.Services.Social.Abstractions;

/// <summary>
/// Analyzes content for style, tone, topics, and sentiment using AI.
/// </summary>
public interface IContentAnalyzer
{
    /// <summary>
    /// Analyzes a single content item
    /// </summary>
    Task<ContentAnalysis> AnalyzeAsync(
        UnifiedContent content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a project-wide writing profile from all content
    /// </summary>
    Task<WritingProfile> GenerateProfileAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the writing profile based on new content
    /// </summary>
    Task UpdateProfileAsync(
        string projectId,
        IEnumerable<UnifiedContent> newContent,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of content analysis
/// </summary>
public class ContentAnalysis
{
    public string ContentId { get; set; } = "";
    public DateTime AnalyzedAt { get; set; }

    // Style Analysis
    public string Tone { get; set; } = ""; // professional, casual, technical, friendly
    public int AvgSentenceLength { get; set; }
    public string VocabularyLevel { get; set; } = ""; // basic, intermediate, advanced
    public List<string> CommonPhrases { get; set; } = new();
    public string EmotionalTone { get; set; } = ""; // positive, negative, neutral

    // Topic Analysis
    public List<string> MainTopics { get; set; } = new();
    public List<string> Subtopics { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
    public Dictionary<string, double> TopicScores { get; set; } = new(); // topic -> confidence

    // Sentiment Analysis
    public string Sentiment { get; set; } = ""; // positive, neutral, negative
    public double SentimentScore { get; set; } // -1.0 to 1.0

    // Engagement Prediction
    public double PredictedEngagementRate { get; set; }
    public List<string> SuggestedImprovements { get; set; } = new();
}

/// <summary>
/// Project-wide writing profile (brand voice)
/// </summary>
public class WritingProfile
{
    public string ProjectId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public int SampleCount { get; set; }

    // Dominant Characteristics
    public string DominantTone { get; set; } = "";
    public int AvgContentLength { get; set; }
    public int AvgSentenceLength { get; set; }
    public string VocabularyLevel { get; set; } = "";

    // Topics & Keywords
    public List<string> TopTopics { get; set; } = new();
    public List<string> BrandKeywords { get; set; } = new();
    public Dictionary<string, int> KeywordFrequency { get; set; } = new();

    // Style Patterns
    public List<string> CommonPhrases { get; set; } = new();
    public List<string> OpeningSentences { get; set; } = new();
    public List<string> ClosingSentences { get; set; } = new();

    // AI-Generated Summary
    public string StyleSummary { get; set; } = "";
}
```

---

## 📅 PHASE 4: CALENDAR ABSTRACTION

### **4.1 IContentCalendarService**

**File:** `Hazina.Tools.Services.Social/Abstractions/IContentCalendarService.cs`

```csharp
namespace Hazina.Tools.Services.Social.Abstractions;

/// <summary>
/// Generic calendar service for all content types
/// </summary>
public interface IContentCalendarService
{
    /// <summary>
    /// Gets calendar events for a date range
    /// </summary>
    Task<List<CalendarEvent>> GetEventsAsync(
        string projectId,
        DateTime startDate,
        DateTime endDate,
        CalendarEventFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a calendar event from content
    /// </summary>
    Task<CalendarEvent> CreateEventAsync(
        string projectId,
        UnifiedContent content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content gaps (dates without published content)
    /// </summary>
    Task<List<DateTime>> GetContentGapsAsync(
        string projectId,
        DateTime startDate,
        DateTime endDate,
        int minDaysGap = 7,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Calendar event model
/// </summary>
public class CalendarEvent
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public CalendarEventType EventType { get; set; }
    public string SourceType { get; set; } = ""; // wordpress, linkedin, etc.
    public string SourceId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public bool IsHistorical { get; set; }
    public bool IsEditable { get; set; }
    public string? EventUrl { get; set; }
    public int? LikeCount { get; set; }
    public int? CommentCount { get; set; }
    public int? ViewCount { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public enum CalendarEventType
{
    WordPressPost,
    WordPressPage,
    WordPressProduct,
    LinkedInPost,
    FacebookPost,
    InstagramPost,
    TwitterPost,
    ScheduledPost,
    DraftPost
}

public class CalendarEventFilter
{
    public List<CalendarEventType>? EventTypes { get; set; }
    public bool? IncludeHistorical { get; set; }
    public bool? IncludeFuture { get; set; }
}
```

---

## 🎨 PHASE 5: AI INSPIRATION ENGINE

### **5.1 IContentInspirationEngine**

**File:** `Hazina.Tools.Services.Social/Abstractions/IContentInspirationEngine.cs`

```csharp
namespace Hazina.Tools.Services.Social.Abstractions;

/// <summary>
/// AI-powered content inspiration engine.
/// Uses past content to inspire new content generation.
/// </summary>
public interface IContentInspirationEngine
{
    /// <summary>
    /// Finds similar past content for inspiration
    /// </summary>
    Task<List<InspirationalContent>> FindSimilarContentAsync(
        string projectId,
        string query,
        int limit = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates content inspired by past content
    /// </summary>
    Task<GeneratedContent> GenerateInspiredContentAsync(
        string projectId,
        ContentGenerationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets inspiration context for AI prompt enhancement
    /// </summary>
    Task<InspirationContext> GetInspirationContextAsync(
        string projectId,
        string topic,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Content that can be used as inspiration
/// </summary>
public class InspirationalContent
{
    public UnifiedContent Content { get; set; } = new();
    public double SimilarityScore { get; set; }
    public List<string> MatchedKeywords { get; set; } = new();
    public string ReasonForMatch { get; set; } = "";
}

/// <summary>
/// Request for AI content generation
/// </summary>
public class ContentGenerationRequest
{
    public string Topic { get; set; } = "";
    public string? DesiredTone { get; set; }
    public ContentLength Length { get; set; } = ContentLength.Medium;
    public bool UseInspiration { get; set; } = true;
    public List<string>? RequiredKeywords { get; set; }
    public string? TargetPlatform { get; set; } // wordpress, linkedin, etc.
}

public enum ContentLength
{
    Short,    // ~100 words
    Medium,   // ~300 words
    Long      // ~500+ words
}

/// <summary>
/// Generated content with inspiration sources
/// </summary>
public class GeneratedContent
{
    public string Content { get; set; } = "";
    public string? Title { get; set; }
    public List<InspirationalContent> InspirationSources { get; set; } = new();
    public string MatchedTone { get; set; } = "";
    public List<string> UsedKeywords { get; set; } = new();
    public string GenerationPrompt { get; set; } = ""; // For debugging/refinement
}

/// <summary>
/// Context for AI prompt enhancement
/// </summary>
public class InspirationContext
{
    public WritingProfile Profile { get; set; } = new();
    public List<UnifiedContent> SimilarContent { get; set; } = new();
    public List<string> SuggestedKeywords { get; set; } = new();
    public List<string> PopularTopics { get; set; } = new();
    public string ContextSummary { get; set; } = "";
}
```

---

## 🔧 PHASE 6: IMPLEMENTATION

### **6.1 Update Existing Providers**

**Update WordPress Provider to use UnifiedContent:**

```csharp
// Before (returns SocialArticle)
Task<List<SocialArticle>> FetchPagesAsync(...);

// After (returns UnifiedContent)
Task<List<UnifiedContent>> FetchPagesAsync(...);

// Mapping example
var unifiedContent = new UnifiedContent
{
    Id = $"wordpress-{wpPage.Id}",
    SourceType = "wordpress",
    SourceId = wpPage.Id.ToString(),
    ContentType = "page",
    Title = StripHtml(wpPage.Title?.Rendered ?? ""),
    Content = wpPage.Content?.Rendered ?? "",
    ContentHtml = wpPage.Content?.Rendered,
    ContentPlainText = StripHtml(wpPage.Content?.Rendered ?? ""),
    FeaturedImageUrl = ExtractFeaturedImage(wpPage),
    PublishedAt = wpPage.Date ?? DateTime.UtcNow,
    UpdatedAt = wpPage.Modified,
    SourceUrl = wpPage.Link,
    Tags = ExtractTags(wpPage),
    Categories = ExtractCategories(wpPage),
    PlatformMetadata = new Dictionary<string, object>
    {
        ["wordpress_id"] = wpPage.Id,
        ["slug"] = wpPage.Slug ?? "",
        ["type"] = "page"
    }
};
```

### **6.2 Database Schema (SQLite Implementation)**

```sql
-- Main unified content table
CREATE TABLE unified_content (
    id TEXT PRIMARY KEY,
    project_id TEXT NOT NULL,
    account_id TEXT NOT NULL,
    source_type TEXT NOT NULL,
    source_id TEXT NOT NULL,
    source_url TEXT,
    content_type TEXT NOT NULL,
    title TEXT,
    content TEXT NOT NULL,
    summary TEXT,
    content_html TEXT,
    content_plaintext TEXT,
    featured_image_url TEXT,
    author_name TEXT,
    author_id TEXT,
    published_at TEXT NOT NULL,
    updated_at TEXT,
    imported_at TEXT NOT NULL,
    last_synced_at TEXT,
    like_count INTEGER DEFAULT 0,
    comment_count INTEGER DEFAULT 0,
    share_count INTEGER DEFAULT 0,
    view_count INTEGER DEFAULT 0,
    engagement_rate REAL DEFAULT 0.0,
    tags TEXT, -- JSON array
    categories TEXT, -- JSON array
    language TEXT,
    detected_topics TEXT, -- JSON array
    sentiment TEXT,
    tone TEXT,
    status TEXT DEFAULT 'published',
    is_historical BOOLEAN DEFAULT 1,
    display_on_calendar BOOLEAN DEFAULT 1,
    is_deleted BOOLEAN DEFAULT 0,
    document_store_id TEXT,
    embedding_id TEXT,
    platform_metadata TEXT, -- JSON
    created_at TEXT NOT NULL
);

CREATE INDEX idx_unified_project ON unified_content(project_id);
CREATE INDEX idx_unified_account ON unified_content(account_id);
CREATE INDEX idx_unified_source ON unified_content(source_type, source_id);
CREATE INDEX idx_unified_published ON unified_content(published_at);
CREATE INDEX idx_unified_calendar ON unified_content(project_id, published_at, display_on_calendar);

-- Media table
CREATE TABLE content_media (
    id TEXT PRIMARY KEY,
    content_id TEXT NOT NULL,
    media_type TEXT NOT NULL,
    url TEXT NOT NULL,
    local_url TEXT,
    thumbnail_url TEXT,
    width INTEGER,
    height INTEGER,
    duration_seconds INTEGER,
    file_size_bytes INTEGER,
    mime_type TEXT,
    display_order INTEGER DEFAULT 0,
    is_featured BOOLEAN DEFAULT 0,
    caption TEXT,
    alt_text TEXT,
    imported_at TEXT NOT NULL,
    FOREIGN KEY (content_id) REFERENCES unified_content(id)
);

-- Comments table
CREATE TABLE content_comments (
    id TEXT PRIMARY KEY,
    content_id TEXT NOT NULL,
    parent_comment_id TEXT,
    source_type TEXT NOT NULL,
    source_comment_id TEXT NOT NULL,
    author_name TEXT NOT NULL,
    author_id TEXT,
    author_avatar_url TEXT,
    comment_text TEXT NOT NULL,
    comment_html TEXT,
    like_count INTEGER DEFAULT 0,
    reply_count INTEGER DEFAULT 0,
    posted_at TEXT NOT NULL,
    imported_at TEXT NOT NULL,
    is_deleted BOOLEAN DEFAULT 0,
    sentiment TEXT,
    is_question BOOLEAN DEFAULT 0,
    requires_response BOOLEAN DEFAULT 0,
    FOREIGN KEY (content_id) REFERENCES unified_content(id)
);

-- Writing profiles table
CREATE TABLE writing_profiles (
    project_id TEXT PRIMARY KEY,
    dominant_tone TEXT,
    avg_content_length INTEGER,
    avg_sentence_length INTEGER,
    vocabulary_level TEXT,
    top_topics TEXT, -- JSON array
    brand_keywords TEXT, -- JSON array
    common_phrases TEXT, -- JSON array
    style_summary TEXT,
    sample_count INTEGER,
    created_at TEXT NOT NULL,
    last_updated_at TEXT NOT NULL
);

-- Full-text search
CREATE VIRTUAL TABLE unified_content_fts USING fts5(
    id,
    title,
    content,
    tags,
    content='unified_content',
    content_rowid='rowid'
);
```

---

## 📦 MIGRATION STRATEGY

### **Backward Compatibility Approach**

1. **Keep old interfaces temporarily** (ISocialContentStore with Posts/Articles)
2. **Add new interfaces** (IUnifiedContentStore)
3. **Implement adapters** (old interfaces delegate to new unified store)
4. **Deprecate gradually** (mark old interfaces as obsolete)

**Example Adapter:**

```csharp
public class SocialContentStoreAdapter : ISocialContentStore
{
    private readonly IUnifiedContentStore _unifiedStore;

    [Obsolete("Use IUnifiedContentStore instead")]
    public async Task SavePostsAsync(
        string projectId,
        string accountId,
        IEnumerable<SocialPost> posts,
        CancellationToken cancellationToken = default)
    {
        var unifiedContent = posts.Select(p => ConvertToUnifiedContent(p, accountId));
        await _unifiedStore.SaveContentAsync(projectId, unifiedContent, cancellationToken);
    }

    private UnifiedContent ConvertToUnifiedContent(SocialPost post, string accountId)
    {
        return new UnifiedContent
        {
            AccountId = accountId,
            SourceId = post.Id,
            Content = post.Content,
            // ... map all fields
        };
    }
}
```

---

## 🚀 ROLLOUT PLAN

### **Week 1: Foundation**
- ✅ Create UnifiedContent model
- ✅ Create IUnifiedContentStore interface
- ✅ Implement SQLite UnifiedContentStore
- ✅ Write unit tests

### **Week 2: Migration**
- ✅ Update WordPress provider to use UnifiedContent
- ✅ Update LinkedIn provider (if exists)
- ✅ Create migration scripts (old schema → new schema)
- ✅ Test with real WordPress data

### **Week 3: Analysis & Calendar**
- ✅ Implement IContentAnalyzer
- ✅ Implement IContentCalendarService
- ✅ Add Document Store integration
- ✅ Test analysis pipeline

### **Week 4: AI Inspiration**
- ✅ Implement IContentInspirationEngine
- ✅ Build WritingProfile generator
- ✅ Enhance AI prompt with inspiration context
- ✅ End-to-end testing

---

## 📄 CLIENT-MANAGER USAGE EXAMPLE

```csharp
// In ClientManagerAPI Startup.cs
services.AddHazinaContent(options =>
{
    options.DatabasePath = "C:/stores/{projectId}/content.db";
    options.DocumentStorePath = "C:/stores/{projectId}/documents";
    options.EnableAnalysis = true;
    options.EnableCalendar = true;
    options.EnableInspiration = true;
});

// In a controller
public class ContentController : ControllerBase
{
    private readonly IUnifiedContentStore _contentStore;
    private readonly IContentCalendarService _calendar;
    private readonly IContentInspirationEngine _inspiration;

    [HttpGet("{projectId}/calendar")]
    public async Task<IActionResult> GetCalendar(
        string projectId,
        [FromQuery] DateTime start,
        [FromQuery] DateTime end)
    {
        var events = await _calendar.GetEventsAsync(projectId, start, end);
        return Ok(events);
    }

    [HttpPost("{projectId}/generate")]
    public async Task<IActionResult> GenerateContent(
        string projectId,
        [FromBody] ContentGenerationRequest request)
    {
        var generated = await _inspiration.GenerateInspiredContentAsync(projectId, request);
        return Ok(generated);
    }
}
```

---

## ✅ SUCCESS CRITERIA

**Framework Level (Hazina):**
- [ ] UnifiedContent model supports ALL platforms
- [ ] IUnifiedContentStore implemented with SQLite
- [ ] Content analysis service functional
- [ ] Calendar service returns events
- [ ] AI inspiration engine generates on-brand content
- [ ] 100% unit test coverage
- [ ] NuGet package published

**Application Level (client-manager):**
- [ ] WordPress import uses unified model
- [ ] Calendar UI shows imported content
- [ ] AI post generator uses inspiration
- [ ] Users report "sounds like my brand"

---

## 🎯 CONCLUSION

This design makes Hazina a **complete content management framework** that any application can use:

- ✅ **Platform-agnostic** - Works with WordPress, LinkedIn, Instagram, etc.
- ✅ **LLM-friendly** - Optimized for AI tool queries
- ✅ **Calendar-ready** - Generic calendar abstraction
- ✅ **AI-powered** - Built-in content analysis and inspiration
- ✅ **Reusable** - Any app can plug in and use

**Ready to implement in Hazina!** 🚀
