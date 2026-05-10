# Hazina Agents

This directory contains autonomous agents for various data collection and processing tasks.

## SourceCollector Agent

The **SourceCollector** agent aggregates news articles from 50+ global RSS feeds across multiple regions and languages.

### Features

- **Global Coverage**: Collects from 50+ RSS feeds across 7 regions:
  - Western (Reuters, BBC, CNN, NYT, Guardian, WashPost, AP, FT, Economist, Le Monde, Der Spiegel, El País)
  - Russian (RT, TASS, RIA Novosti, Sputnik, Interfax)
  - Chinese (Xinhua, Global Times, CGTN, China Daily, People's Daily)
  - Middle East (Al Jazeera, Al Arabiya, Times of Israel, Haaretz, Middle East Eye, The National UAE, Daily Sabah)
  - Africa (Nation Africa, The Citizen Kenya, News24, Daily Maverick, AllAfrica, The East African)
  - Latin America (Telesur, Prensa Latina, Clarín, Folha de S.Paulo, El Universal, La Nación)
  - Southeast Asia (Channel NewsAsia, Jakarta Post, Bangkok Post, The Straits Times, Philippine Star, VnExpress)
  - South Asia (The Hindu, Times of India, Dawn Pakistan, Daily Star Bangladesh)
  - East Asia (Japan Times, Korea Herald, Yonhap News)

- **Multi-language Support**: English, Russian, Chinese, Arabic, Spanish, Portuguese, French, German, Vietnamese

- **Duplicate Detection**: URL-based hashing prevents duplicate articles

- **Error Handling**: Gracefully handles:
  - Unreachable feeds (network errors)
  - Timeout errors
  - Malformed RSS feeds
  - Individual article parsing failures

- **Performance**: Target <5 minutes for complete collection from all 50+ feeds

- **Parallel Collection**: Concurrent fetching from all sources

### Data Model

#### Article

```csharp
public class Article
{
    public string Id { get; set; }              // Unique identifier
    public string Title { get; set; }           // Article title
    public string Content { get; set; }         // Article content/summary
    public string Url { get; set; }             // Original URL
    public string Publisher { get; set; }       // Source name
    public string Region { get; set; }          // Geographic region
    public string Language { get; set; }        // ISO 639-1 language code
    public DateTime PublishTime { get; set; }   // Publication time
    public DateTime CollectedAt { get; set; }   // Collection time
    public string? Author { get; set; }         // Author(s)
    public List<string> Categories { get; set; } // Categories/tags
    public string UrlHash { get; set; }         // SHA256 hash for deduplication
}
```

#### RssFeedSource

```csharp
public class RssFeedSource
{
    public required string Publisher { get; set; }   // Publisher name
    public required string FeedUrl { get; set; }     // RSS feed URL
    public required string Region { get; set; }      // Geographic region
    public required string Language { get; set; }    // Language code
    public bool IsActive { get; set; }               // Active/inactive flag
}
```

### Usage

```csharp
using Hazina.Agents.Tools.Agents;
using Microsoft.Extensions.Logging;

// Setup
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<SourceCollector>();
using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

// Create collector
var collector = new SourceCollector(logger, httpClient);

// Collect articles
var articles = await collector.CollectArticlesAsync();

// Get statistics
var stats = collector.GetCollectionStatistics(articles);
Console.WriteLine($"Collected {stats["TotalArticles"]} articles");
Console.WriteLine($"From {stats["UniquePublishers"]} publishers");
Console.WriteLine($"Across {stats["UniqueRegions"]} regions");

// Filter by region
var middleEastArticles = articles
    .Where(a => a.Region == "Middle East")
    .ToList();

// Filter by language
var englishArticles = articles
    .Where(a => a.Language == "en")
    .ToList();
```

### API

#### GetRssFeedSources()

Returns all configured RSS feed sources.

```csharp
List<RssFeedSource> sources = collector.GetRssFeedSources();
```

#### CollectArticlesAsync(CancellationToken)

Collects articles from all active RSS feeds.

```csharp
var articles = await collector.CollectArticlesAsync(cancellationToken);
```

#### GetCollectionStatistics(List<Article>)

Returns collection statistics.

```csharp
var stats = collector.GetCollectionStatistics(articles);
// Returns: TotalArticles, UniquePublishers, UniqueRegions, UniqueLanguages, DuplicatesSkipped
```

### Examples

See `Examples/SourceCollectorExample.cs` for complete examples:

- Basic collection and statistics
- Filtering by region
- Filtering by language

### Testing

Collection time should be <5 minutes for all 50+ feeds:

```bash
cd src/Hazina.Agents.Tools
dotnet build
dotnet test # Once tests are added
```

### Integration

Store collected articles in Hazina state/database for downstream analysis:

- Event detection
- Sentiment analysis
- Topic clustering
- Perspective comparison
- Bias detection

### Future Enhancements

- Database persistence
- Incremental updates (fetch only new articles)
- Content extraction from article URLs (full text vs RSS summary)
- Image/media attachment handling
- Custom feed source configuration
- Rate limiting per source
- Retry logic with exponential backoff
- Feed health monitoring
- Article classification/tagging
