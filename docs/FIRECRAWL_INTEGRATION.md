# FireCrawl MCP Integration - Documentation

**Date Created:** 2026-01-12
**Version:** 1.0
**Status:** Active

## Overview

FireCrawl MCP integration enables Hazina agents to perform autonomous web scraping, branding extraction, site mapping, and structured data extraction through natural language. This transforms Hazina into a powerful web intelligence tool without requiring coding for each scraping operation.

## Key Features

1. **Web Scraping** - Pull webpage content and convert to clean markdown
2. **Site Mapping** - Discover website structure and all internal links
3. **Web Crawling** - Traverse multiple linked pages automatically
4. **Data Extraction** - Extract structured data using AI (branding, product info, etc.)
5. **Branding Analysis** - Automatically extract colors, fonts, logos, typography
6. **Screenshot Capture** - Full-page screenshots for documentation
7. **Content Search** - Search within websites for specific information

## Architecture

### Placement in Hazina

```
[User/Agent]
   |
Natural Language Request
   |
Reasoning Layer
   |
Tool Services (FireCrawlService)
   |
FireCrawl HTTP API
   |
Web Targets
```

FireCrawl is implemented as a **Tool Service** in `Hazina.Tools.Services.Web`, following the same pattern as `WebSearchService` and `WebScrapingService`.

### Components

**Location:** `src/Tools/Services/Hazina.Tools.Services.Web/`

1. **Models** (`Models/FireCrawlModels.cs`)
   - `FireCrawlConfig` - Configuration (API key, base URL)
   - `FireCrawlScrapeResult` - Single page scrape result
   - `FireCrawlMapResult` - Site structure map
   - `FireCrawlCrawlRequest` / `FireCrawlCrawlResult` - Multi-page crawling
   - `FireCrawlExtractRequest` / `FireCrawlExtractResult` - Structured data extraction
   - `FireCrawlScreenshotRequest` / `FireCrawlScreenshotResult` - Screenshot capture
   - `BrandingData` - Extracted branding information
   - `FireCrawlSearchRequest` / `FireCrawlSearchResult` - Content search

2. **Interface** (`Abstractions/IFireCrawlService.cs`)
   - `ScrapeAsync()` - Scrape single page
   - `MapAsync()` - Map site structure
   - `CrawlAsync()` - Crawl multiple pages
   - `ExtractAsync()` - Extract structured data
   - `ExtractBrandingAsync()` - Extract branding (convenience method)
   - `ScreenshotAsync()` - Capture screenshot
   - `SearchAsync()` - Search within site

3. **Implementation** (`FireCrawlService.cs`)
   - HTTP client wrapper for FireCrawl API
   - JSON serialization/deserialization
   - Error handling and logging
   - Result normalization

## Use Cases in Hazina Ecosystem

### 1. Brand2Boost - Automated Branding Extraction

**User Request:** "Import branding from tesla.com for a marketing campaign"

**Workflow:**
```csharp
var fireCrawl = new FireCrawlService(config);

// Extract branding
var result = await fireCrawl.ExtractBrandingAsync("https://tesla.com");

if (result.Success && result.Branding != null)
{
    // Feed to Brand2Boost generators
    var branding = result.Branding;
    // branding.Colors = ["#E82127", "#000000", "#FFFFFF"]
    // branding.Fonts = ["Gotham", "Arial"]
    // branding.PrimaryColor = "#E82127"
    // branding.LogoUrl = "https://tesla.com/logo.svg"

    // Auto-generate:
    // - Color palettes
    // - Typography sets
    // - Logo detection
    // - Brand guidelines
}
```

### 2. Competitive Analysis & Market Research

**User Request:** "Analyze competitor pricing and features from their website"

**Workflow:**
```csharp
// Map competitor site structure
var map = await fireCrawl.MapAsync("https://competitor.com", maxDepth: 2);

// Extract structured pricing data
var pricingExtract = await fireCrawl.ExtractAsync(new FireCrawlExtractRequest
{
    Url = "https://competitor.com/pricing",
    ExtractionPrompt = "Extract all pricing tiers, features, and costs",
    Schema = new Dictionary<string, string>
    {
        { "tiers", "array of pricing tier objects" },
        { "features", "array of feature descriptions" },
        { "prices", "array of price points" }
    }
});

// Store in DocumentStore for RAG
```

### 3. Client Onboarding & Site Audits

**User Request:** "Audit client's existing website and create brand inventory"

**Workflow:**
```csharp
// Full site crawl
var crawl = await fireCrawl.CrawlAsync(new FireCrawlCrawlRequest
{
    Url = "https://client-site.com",
    MaxDepth = 3,
    MaxPages = 25,
    IncludeScreenshots = true
});

// Extract branding from homepage
var branding = await fireCrawl.ExtractBrandingAsync("https://client-site.com");

// Generate audit report with:
// - All pages crawled
// - Current branding (colors, fonts)
// - Screenshots for documentation
// - Recommendations for improvement
```

### 4. Knowledge Graph Construction

**User Request:** "Build knowledge graph of Palantir's product offerings"

**Workflow:**
```csharp
// Map site structure
var map = await fireCrawl.MapAsync("https://palantir.com", maxDepth: 2);

// Crawl product pages
var crawl = await fireCrawl.CrawlAsync(new FireCrawlCrawlRequest
{
    Url = "https://palantir.com/products",
    MaxDepth = 2,
    MaxPages = 20
});

// For each page:
// - Extract structured product info
// - Embed content → DocumentStore
// - Build knowledge graph edges
//   - nodes = pages
//   - edges = internal links
//   - semantic edges via LLM tagging
```

### 5. RFP & Procurement Intelligence

**User Request:** "Research vendor capabilities for RFP response"

**Workflow:**
```csharp
// Search vendor site for specific capabilities
var search = await fireCrawl.SearchAsync(new FireCrawlSearchRequest
{
    Query = "enterprise security compliance SOC2 HIPAA",
    Domain = "vendor-site.com",
    MaxResults = 10
});

// Extract from relevant pages
foreach (var result in search.Results)
{
    var extracted = await fireCrawl.ExtractAsync(new FireCrawlExtractRequest
    {
        Url = result.Url,
        ExtractionPrompt = "Extract security certifications, compliance standards, and capabilities"
    });

    // Store for RAG-based RFP generation
}
```

### 6. Portfolio & Asset Management

**User Request:** "Import screenshots and branding from our portfolio sites"

**Workflow:**
```csharp
var portfolioSites = new[] { "site1.com", "site2.com", "site3.com" };

foreach (var site in portfolioSites)
{
    // Screenshot
    var screenshot = await fireCrawl.ScreenshotAsync(new FireCrawlScreenshotRequest
    {
        Url = $"https://{site}",
        FullPage = true,
        Width = 1920,
        Height = 1080
    });

    // Store screenshot → AssetStore or BinaryStore
    await assetStore.SaveAsync($"{site}-screenshot.png",
        Convert.FromBase64String(screenshot.Screenshot));

    // Extract branding
    var branding = await fireCrawl.ExtractBrandingAsync($"https://{site}");

    // Store in portfolio database
}
```

## Configuration

### Setup

1. **Obtain FireCrawl API Key**
   - Sign up at https://firecrawl.dev
   - Get API key from dashboard

2. **Configure in Hazina**

```csharp
// In appsettings.json or environment variables
{
  "FireCrawl": {
    "ApiKey": "fc-your-api-key-here",
    "BaseUrl": "https://api.firecrawl.dev/v1"
  }
}

// In service registration (Program.cs or Startup.cs)
services.AddSingleton<IFireCrawlService>(sp =>
{
    var config = new FireCrawlConfig
    {
        ApiKey = configuration["FireCrawl:ApiKey"],
        BaseUrl = configuration["FireCrawl:BaseUrl"] ?? "https://api.firecrawl.dev/v1"
    };

    var logger = sp.GetRequiredService<ILogger<FireCrawlService>>();

    return new FireCrawlService(
        config,
        httpClient: null, // Uses new HttpClient
        logInfo: msg => logger.LogInformation(msg),
        logError: (ex, msg) => logger.LogError(ex, msg)
    );
});
```

## API Reference

### ScrapeAsync

```csharp
Task<FireCrawlScrapeResult> ScrapeAsync(string url, bool includeHtml = false)
```

**Purpose:** Scrape a single webpage, convert HTML to markdown.

**Parameters:**
- `url` - URL to scrape
- `includeHtml` - Include raw HTML in response (default: false)

**Returns:**
- `Markdown` - Clean markdown content
- `Html` - Raw HTML (if requested)
- `Metadata` - Page metadata (title, description, etc.)

**Example:**
```csharp
var result = await fireCrawl.ScrapeAsync("https://example.com");
if (result.Success)
{
    Console.WriteLine(result.Markdown); // Clean markdown
}
```

---

### MapAsync

```csharp
Task<FireCrawlMapResult> MapAsync(string baseUrl, int maxDepth = 2)
```

**Purpose:** Discover website structure and all internal links.

**Parameters:**
- `baseUrl` - Base URL to map
- `maxDepth` - Maximum depth to traverse (default: 2)

**Returns:**
- `Links` - List of all discovered URLs
- `Structure` - Hierarchical site structure

**Example:**
```csharp
var map = await fireCrawl.MapAsync("https://example.com", maxDepth: 3);
Console.WriteLine($"Found {map.Links.Count} pages");
```

---

### CrawlAsync

```csharp
Task<FireCrawlCrawlResult> CrawlAsync(FireCrawlCrawlRequest request)
```

**Purpose:** Crawl multiple linked pages and collect content.

**Parameters:**
- `request.Url` - Starting URL
- `request.MaxDepth` - Maximum crawl depth (default: 2)
- `request.MaxPages` - Maximum pages to crawl (default: 10)
- `request.IncludeScreenshots` - Capture screenshots (default: false)
- `request.AllowedDomains` - Restrict crawling to specific domains

**Returns:**
- `Pages` - Collection of crawled pages with content
- `PagesCrawled` - Number of pages successfully crawled

**Example:**
```csharp
var crawl = await fireCrawl.CrawlAsync(new FireCrawlCrawlRequest
{
    Url = "https://example.com",
    MaxDepth = 2,
    MaxPages = 20,
    IncludeScreenshots = false
});

foreach (var page in crawl.Pages)
{
    Console.WriteLine($"{page.Title}: {page.Url}");
    // page.Markdown contains clean content
}
```

---

### ExtractAsync

```csharp
Task<FireCrawlExtractResult> ExtractAsync(FireCrawlExtractRequest request)
```

**Purpose:** Extract structured data using AI.

**Parameters:**
- `request.Url` - URL to extract from
- `request.ExtractionPrompt` - Natural language extraction instructions
- `request.Schema` - Optional schema definition for structured output

**Returns:**
- `ExtractedData` - Structured data dictionary
- `Branding` - Branding data (if extracting branding)

**Example:**
```csharp
var extract = await fireCrawl.ExtractAsync(new FireCrawlExtractRequest
{
    Url = "https://example.com/pricing",
    ExtractionPrompt = "Extract all pricing tiers with features and prices",
    Schema = new Dictionary<string, string>
    {
        { "tiers", "array of tier objects" },
        { "features", "array of feature lists" },
        { "prices", "array of price points" }
    }
});

// Access structured data
var tiers = extract.ExtractedData["tiers"];
```

---

### ExtractBrandingAsync

```csharp
Task<FireCrawlExtractResult> ExtractBrandingAsync(string url)
```

**Purpose:** Convenience method to extract branding (colors, fonts, logo).

**Parameters:**
- `url` - URL to extract branding from

**Returns:**
- `Branding.Colors` - List of hex color codes
- `Branding.Fonts` - List of font families
- `Branding.PrimaryColor` - Primary brand color (hex)
- `Branding.SecondaryColor` - Secondary brand color (hex)
- `Branding.LogoUrl` - URL of company logo
- `Branding.Typography` - Font assignments (heading, body, etc.)

**Example:**
```csharp
var result = await fireCrawl.ExtractBrandingAsync("https://example.com");
if (result.Success && result.Branding != null)
{
    Console.WriteLine($"Primary Color: {result.Branding.PrimaryColor}");
    Console.WriteLine($"Fonts: {string.Join(", ", result.Branding.Fonts)}");
}
```

---

### ScreenshotAsync

```csharp
Task<FireCrawlScreenshotResult> ScreenshotAsync(FireCrawlScreenshotRequest request)
```

**Purpose:** Capture full-page screenshot.

**Parameters:**
- `request.Url` - URL to screenshot
- `request.FullPage` - Capture entire page vs viewport only (default: true)
- `request.Width` - Viewport width (default: 1280)
- `request.Height` - Viewport height (default: 720)

**Returns:**
- `Screenshot` - Base64-encoded PNG image
- `MimeType` - Image format (always "image/png")

**Example:**
```csharp
var screenshot = await fireCrawl.ScreenshotAsync(new FireCrawlScreenshotRequest
{
    Url = "https://example.com",
    FullPage = true,
    Width = 1920,
    Height = 1080
});

if (screenshot.Success)
{
    var imageBytes = Convert.FromBase64String(screenshot.Screenshot);
    await File.WriteAllBytesAsync("screenshot.png", imageBytes);
}
```

---

### SearchAsync

```csharp
Task<FireCrawlSearchResult> SearchAsync(FireCrawlSearchRequest request)
```

**Purpose:** Search within a website for specific content.

**Parameters:**
- `request.Query` - Search query
- `request.Domain` - Optional domain to restrict search
- `request.MaxResults` - Maximum results to return (default: 5)

**Returns:**
- `Results` - List of matching pages with snippets
- `ResultCount` - Number of results found

**Example:**
```csharp
var search = await fireCrawl.SearchAsync(new FireCrawlSearchRequest
{
    Query = "pricing enterprise plan",
    Domain = "example.com",
    MaxResults = 10
});

foreach (var result in search.Results)
{
    Console.WriteLine($"{result.Title}: {result.Url}");
    Console.WriteLine($"  {result.Snippet}");
    Console.WriteLine($"  Relevance: {result.Relevance}");
}
```

## Integration Patterns

### Pattern 1: Scrape → Embed → Store

```csharp
// Scrape content
var scrape = await fireCrawl.ScrapeAsync(url);

// Normalize to markdown
var markdown = scrape.Markdown;

// Embed with Hazina embedding provider
var embedding = await embeddingService.EmbedAsync(markdown);

// Store in DocumentStore
await documentStore.AddAsync(new Document
{
    Id = Guid.NewGuid().ToString(),
    Content = markdown,
    Embedding = embedding,
    Metadata = new Dictionary<string, object>
    {
        { "source", url },
        { "type", "web-scrape" },
        { "scraped_at", DateTime.UtcNow }
    }
});
```

### Pattern 2: Map → Filter → Crawl

```csharp
// Map entire site
var map = await fireCrawl.MapAsync(baseUrl, maxDepth: 3);

// Filter for relevant pages (e.g., only /products/* URLs)
var productPages = map.Links
    .Where(url => url.Contains("/products/"))
    .Take(20)
    .ToList();

// Crawl filtered pages
foreach (var url in productPages)
{
    var scrape = await fireCrawl.ScrapeAsync(url);
    // Process content...
}
```

### Pattern 3: Extract → Transform → Knowledge Graph

```csharp
// Extract structured data
var extract = await fireCrawl.ExtractAsync(request);

// Transform to knowledge graph nodes/edges
var node = new KnowledgeNode
{
    Id = Guid.NewGuid(),
    Type = "Product",
    Properties = extract.ExtractedData
};

// Add edges based on extracted relationships
await graphStore.AddNodeAsync(node);
```

### Pattern 4: Branding → Brand2Boost Pipeline

```csharp
// Extract branding
var branding = await fireCrawl.ExtractBrandingAsync(url);

// Feed to Brand2Boost generators
var colorPalette = await brand2boost.GeneratePaletteAsync(branding.Branding.Colors);
var typographySet = await brand2boost.GenerateTypographyAsync(branding.Branding.Fonts);
var logoVariations = await brand2boost.GenerateLogoVariationsAsync(branding.Branding.LogoUrl);

// Generate brand guidelines document
var brandGuide = await brand2boost.GenerateBrandGuideAsync(new BrandGuideRequest
{
    Colors = branding.Branding.Colors,
    Fonts = branding.Branding.Fonts,
    Logo = branding.Branding.LogoUrl,
    Typography = branding.Branding.Typography
});
```

## Error Handling

All methods return result objects with `Success` and `Error` properties:

```csharp
var result = await fireCrawl.ScrapeAsync(url);

if (!result.Success)
{
    Console.WriteLine($"Error: {result.Error}");
    // Handle error (retry, log, notify user, etc.)
}
else
{
    // Process successful result
    var content = result.Markdown;
}
```

**Common Errors:**
- **401 Unauthorized** - Invalid API key
- **404 Not Found** - URL does not exist
- **429 Rate Limited** - Too many requests
- **500 Server Error** - FireCrawl API issue
- **Timeout** - Page took too long to load

## Performance Considerations

### Rate Limiting

FireCrawl API has rate limits. Implement throttling for bulk operations:

```csharp
var semaphore = new SemaphoreSlim(5); // Max 5 concurrent requests

foreach (var url in urls)
{
    await semaphore.WaitAsync();

    _ = Task.Run(async () =>
    {
        try
        {
            var result = await fireCrawl.ScrapeAsync(url);
            // Process result...
        }
        finally
        {
            semaphore.Release();
        }
    });
}
```

### Caching

Cache scraped content to avoid redundant API calls:

```csharp
// Check cache first
var cachedContent = await cache.GetAsync<string>($"scrape:{url}");
if (cachedContent != null)
{
    return cachedContent;
}

// Scrape if not cached
var result = await fireCrawl.ScrapeAsync(url);
if (result.Success)
{
    await cache.SetAsync($"scrape:{url}", result.Markdown, TimeSpan.FromHours(24));
}
```

### Large Crawls

For crawling 100+ pages, use async processing:

```csharp
// Queue crawl jobs
foreach (var url in largeSiteMap.Links)
{
    await queue.EnqueueAsync(new CrawlJob { Url = url });
}

// Background worker processes queue
public class CrawlWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var job = await queue.DequeueAsync(stoppingToken);
            var result = await fireCrawl.ScrapeAsync(job.Url);
            await ProcessResultAsync(result);
        }
    }
}
```

## Benefits for Hazina Ecosystem

| Benefit | Impact |
|---------|--------|
| **Data Autonomy** | No reliance on provided corpora, agents operate on live web data |
| **Live Intelligence** | Real-time competitive analysis, pricing updates, market research |
| **Zero-Code Input** | Lowers user barrier - natural language queries vs manual scraping code |
| **Structured Ingestion** | Clean markdown + structured extraction fuels knowledge graphs & RAG |
| **Multi-Modal** | Text + screenshots + branding data for comprehensive analysis |
| **Agent Autonomy** | Agents can research web autonomously during task execution |
| **Brand2Boost Integration** | Automatic branding extraction → generative marketing workflows |
| **Knowledge Graph** | Site structure mapping → graph construction → semantic search |

## Future Enhancements

**Planned Features:**

1. **Incremental Crawling** - Track changes over time, only re-scrape updated pages
2. **JavaScript Rendering** - Handle SPAs and dynamic content (React, Vue, Angular)
3. **Form Interaction** - Submit forms, login flows for authenticated scraping
4. **PDF Extraction** - Extract data from PDF documents found during crawls
5. **Video/Audio Transcription** - Extract transcripts from embedded media
6. **Proxy Support** - Route requests through proxies for geo-specific content
7. **CAPTCHA Solving** - Integrate CAPTCHA solving for protected sites

## Support & Troubleshooting

### API Key Issues

**Symptom:** 401 Unauthorized errors
**Solution:** Verify API key is correctly set in configuration

```bash
# Check configuration
dotnet user-secrets list | grep FireCrawl
```

### Timeout Errors

**Symptom:** HttpClient timeout exceptions
**Solution:** Increase timeout for slow-loading sites

```csharp
var config = new FireCrawlConfig { ApiKey = "...", BaseUrl = "..." };
var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
var service = new FireCrawlService(config, httpClient);
```

### Empty Results

**Symptom:** Success = true but no data extracted
**Solution:** Check extraction prompt specificity

```csharp
// Too vague
ExtractionPrompt = "Extract data"

// Better
ExtractionPrompt = "Extract all pricing tiers with: tier name, monthly price, annual price, and feature list"
```

### Rate Limiting

**Symptom:** 429 Rate Limited errors
**Solution:** Implement exponential backoff

```csharp
int retryCount = 0;
while (retryCount < 3)
{
    var result = await fireCrawl.ScrapeAsync(url);
    if (result.Success) break;

    if (result.Error?.Contains("429") == true)
    {
        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
        retryCount++;
    }
    else
    {
        break; // Different error, don't retry
    }
}
```

## References

- **FireCrawl Documentation:** https://docs.firecrawl.dev
- **FireCrawl API Reference:** https://docs.firecrawl.dev/api-reference
- **Hazina Architecture:** https://github.com/martiendejong/Hazina
- **MCP (Model Context Protocol):** https://modelcontextprotocol.io

## Changelog

**v1.0 (2026-01-12)**
- Initial implementation
- All 7 core operations (scrape, map, crawl, extract, branding, screenshot, search)
- Complete documentation with use cases and examples
- Integration patterns for Hazina ecosystem

---

**Implemented by:** Claude Sonnet 4.5
**Review Status:** Ready for PR
**License:** Same as Hazina project
