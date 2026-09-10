# Article Generator Agent

**AI-powered article generation for event-driven journalism**

## Overview

This example demonstrates how to build a professional article generation agent using Hazina and GPT-4. The agent transforms structured event data into readable, neutral, fact-based news articles formatted for WordPress publishing.

## Features

✅ **Structured Content Generation**
- Title, Summary, Facts, Narratives, Faction Analysis
- Follows professional journalism template
- HTML output ready for WordPress

✅ **Quality Assurance**
- Readability scoring (Flesch-Kincaid)
- Bias language detection
- Neutrality validation
- Completeness checks

✅ **Professional Standards**
- Human-readable, accessible language
- Neutral tone maintained
- Multiple perspectives presented
- Fact-based approach

✅ **WordPress Integration**
- Valid HTML output
- Semantic tags (<article>, <section>, <h1-h3>)
- Ready for REST API publishing
- SEO-friendly structure

## Quick Start

### Prerequisites

- .NET 8.0 or higher (.NET 9.0 recommended)
- OpenAI API key with GPT-4 access

### Setup

```bash
# 1. Set your API key
export OPENAI_API_KEY=sk-your-key-here  # Linux/Mac
set OPENAI_API_KEY=sk-your-key-here     # Windows

# 2. Run the example
cd docs/examples/06-article-generator
dotnet run
```

## Usage

### Basic Article Generation

```csharp
using Hazina.AI.FluentAPI.Configuration;
using Hazina.Examples.ArticleGenerator;

// Setup
var ai = QuickSetup.SetupOpenAI(apiKey);
var generator = new ArticleGenerator(ai);

// Create event data
var eventData = new EventData
{
    Title = "Senate Passes Climate Legislation",
    Date = DateTime.Parse("2024-06-15"),
    EventType = "Politics",
    Facts = new List<string>
    {
        "Senate voted 52-48 to pass comprehensive climate bill",
        "Legislation allocates $400 billion over 10 years",
        "Key provisions include renewable energy tax credits"
    },
    Narratives = new List<string>
    {
        "Supporters celebrate historic climate action",
        "Critics argue bill doesn't go far enough"
    },
    Factions = new List<FactionInfo>
    {
        new() {
            Name = "Democratic Senators",
            Position = "Strong support, highlighting job creation"
        },
        new() {
            Name = "Republican Senators",
            Position = "Opposition, citing economic concerns"
        }
    }
};

// Generate article
var article = await generator.GenerateArticleAsync(eventData);

// Validate quality
var validation = generator.ValidateArticle(article);

if (validation.IsValid)
{
    Console.WriteLine($"Article ready! ({validation.WordCount} words)");
    Console.WriteLine($"Readability: {validation.ReadabilityScore:F1}/100");
}
```

### WordPress Publishing

```csharp
// Get the HTML
var htmlContent = article.RawHtml;

// POST to WordPress REST API
var wordPressApi = "https://your-site.com/wp-json/wp/v2/posts";
var payload = new
{
    title = article.Title,
    content = htmlContent,
    status = "draft",  // Review before publishing
    categories = new[] { 1 },  // Your category ID
    tags = new[] { 2, 3 }  // Your tag IDs
};

// Use your preferred HTTP client
// var response = await httpClient.PostAsJsonAsync(wordPressApi, payload);
```

## Article Structure

Generated articles follow this template:

```html
<article>
  <h1>[Compelling, Neutral Title]</h1>

  <section class="summary">
    <p>[2-3 sentence executive summary]</p>
  </section>

  <section class="facts">
    <h2>What Happened</h2>
    <ul>
      <li>[Verifiable fact 1]</li>
      <li>[Verifiable fact 2]</li>
      <li>[Additional facts...]</li>
    </ul>
  </section>

  <section class="narratives">
    <h2>Perspectives</h2>
    <p>[Narrative paragraph presenting viewpoint 1]</p>
    <p>[Narrative paragraph presenting viewpoint 2]</p>
  </section>

  <section class="factions">
    <h2>Stakeholders</h2>
    <div class="faction">
      <h3>[Faction Name]</h3>
      <p>[Their position and interests]</p>
    </div>
  </section>
</article>
```

## Quality Validation

The agent validates articles against these criteria:

### Readability (Flesch-Kincaid Score)
- **Target**: 60-70 (standard readability)
- **90-100**: Very easy (5th grade)
- **60-70**: Standard (8th-9th grade) ✓
- **30-50**: Difficult (college)
- **0-30**: Very difficult (graduate)

### Neutrality
Detects bias words:
- "clearly", "obviously", "undoubtedly"
- "always", "never"
- "everyone knows"

### Completeness
- Title present
- Summary present (2-3 sentences)
- Facts section (minimum 3 facts)
- Narratives section
- Valid HTML structure

### Length
- **Minimum**: 200 words
- **Maximum**: 2000 words
- **Optimal**: 400-800 words

## Example Output

### Input Event Data

```csharp
Title: "Major AI Breakthrough in Medical Diagnostics"
EventType: "Technology"
Facts: [
  "AI system achieves 95% accuracy in cancer detection",
  "Processes imaging 10x faster than radiologists",
  "FDA fast-track approval granted"
]
```

### Generated Article

```html
<article>
  <h1>AI System Achieves Breakthrough in Early Cancer Detection</h1>

  <section class="summary">
    <p>Researchers announced a new artificial intelligence system
    that detects cancer with 95% accuracy, processing medical images
    ten times faster than human radiologists. The FDA has granted
    fast-track approval for clinical trials.</p>
  </section>

  <section class="facts">
    <h2>What Happened</h2>
    <ul>
      <li>AI system demonstrates 95% accuracy in early cancer detection</li>
      <li>System analyzes medical imaging 10x faster than radiologists</li>
      <li>FDA grants fast-track approval for clinical trials</li>
    </ul>
  </section>

  <section class="narratives">
    <h2>Perspectives</h2>
    <p>Medical professionals have expressed optimism about the technology's
    potential to improve early intervention rates...</p>
  </section>
</article>
```

**Quality Metrics**:
- ✓ Readability: 67.3 (target: 60-70)
- ✓ Word Count: 456
- ✓ No bias language detected
- ✓ All sections complete

## Customization

### Adjust Reading Level

```csharp
// Modify system prompt to target different audiences
var systemPrompt = @"
Target audience: Technical professionals
Reading level: College (Flesch-Kincaid 40-50)
Use domain-specific terminology
";
```

### Add Custom Validation

```csharp
public class CustomArticleValidator
{
    public bool CheckFactSources(GeneratedArticle article)
    {
        // Verify each fact has attribution
        foreach (var fact in article.Facts)
        {
            if (!fact.Contains("according to") &&
                !fact.Contains("reported by"))
            {
                return false;
            }
        }
        return true;
    }
}
```

### Different Event Types

```csharp
// Sports event
var sportsEvent = new EventData
{
    EventType = "Sports",
    Facts = new[] { "Team A won 3-2", "Game-winning goal in overtime" },
    // ...
};

// Business event
var businessEvent = new EventData
{
    EventType = "Business",
    Facts = new[] { "Company reports Q2 revenue", "Stock price increases 5%" },
    // ...
};
```

## Integration Patterns

### Event Pipeline Integration

```csharp
// 1. Event detected
var rawEvent = await eventMonitor.GetLatestEventAsync();

// 2. Event analyzed
var analyzedData = await eventAnalyzer.AnalyzeAsync(rawEvent);

// 3. Article generated
var article = await articleGenerator.GenerateArticleAsync(analyzedData);

// 4. Quality checked
var validation = articleGenerator.ValidateArticle(article);

if (validation.IsValid)
{
    // 5. Published to WordPress
    await wordPressPublisher.PublishAsync(article);
}
else
{
    // 6. Flagged for human review
    await humanReviewQueue.AddAsync(article, validation.Issues);
}
```

### Batch Processing

```csharp
var events = await eventStore.GetUnprocessedEventsAsync();

var articles = await Task.WhenAll(
    events.Select(e => articleGenerator.GenerateArticleAsync(e))
);

foreach (var article in articles.Where(a =>
    articleGenerator.ValidateArticle(a).IsValid))
{
    await publisher.PublishAsync(article);
}
```

## Best Practices

### Data Quality
- Provide at least 3 facts per event
- Include multiple perspectives (minimum 2)
- Specify all major stakeholders
- Use accurate dates and event types

### Fact Verification
- All facts must be verifiable
- Include sources when available
- Distinguish facts from opinions
- Attribute claims to sources

### Neutrality
- Present all sides fairly
- Avoid loaded language
- Use attribution ("according to", "claimed")
- Don't editorialize in facts section

### Performance
- Cache LLM client instances
- Process events in batches when possible
- Use async/await throughout
- Monitor generation times

## Troubleshooting

### Low Readability Score

**Problem**: Score < 50 (too difficult)

**Solutions**:
- Simplify sentence structure
- Use shorter words
- Break long paragraphs
- Add examples

### Bias Detected

**Problem**: Validation flags bias language

**Solutions**:
- Remove absolute terms (always, never)
- Use attribution
- Present multiple viewpoints
- Stick to verifiable facts

### Generation Too Slow

**Problem**: >5 seconds per article

**Solutions**:
- Use GPT-3.5 instead of GPT-4
- Reduce event data size
- Batch multiple requests
- Implement caching

## Related Examples

- [05-agent-orchestration](../05-agent-orchestration/) - Multi-agent workflows
- [04-basic-rag](../04-basic-rag/) - Document retrieval for fact-checking
- [02-provider-switching](../02-provider-switching/) - Provider selection strategies

## License

MIT License - see root LICENSE file

## Contributing

See [CONTRIBUTING.md](../../../CONTRIBUTING.md) for guidelines

---

**Questions?** Open an issue on GitHub or check the [documentation](../../).
