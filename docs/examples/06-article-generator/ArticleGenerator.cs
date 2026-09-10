using Hazina.LLMs;
using System.Text;

namespace Hazina.Examples.ArticleGenerator;

/// <summary>
/// AI-powered article generator that creates readable event articles from analyzed data.
///
/// Features:
/// - GPT-4 powered content generation
/// - Structured template (Title, Summary, Facts, Narratives, Faction Analysis)
/// - HTML output for WordPress publishing
/// - Human-readable, neutral tone
/// - Fact-based journalism approach
/// - Flesch-Kincaid readability optimization
/// </summary>
public class ArticleGenerator
{
    private readonly ILLMClient _llm;
    private readonly string _systemPrompt;

    public ArticleGenerator(ILLMClient llm)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _systemPrompt = BuildSystemPrompt();
    }

    /// <summary>
    /// Generate a complete article from event data.
    /// </summary>
    /// <param name="eventData">Structured event data containing facts, narratives, and faction information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated article in HTML format</returns>
    public async Task<GeneratedArticle> GenerateArticleAsync(
        EventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData == null)
            throw new ArgumentNullException(nameof(eventData));

        var startTime = DateTime.UtcNow;

        // Build the generation prompt with event data
        var userPrompt = BuildGenerationPrompt(eventData);

        // Call LLM to generate article
        var messages = new List<HazinaChatMessage>
        {
            new() { Role = HazinaMessageRole.System, Text = _systemPrompt },
            new() { Role = HazinaMessageRole.User, Text = userPrompt }
        };

        var response = await _llm.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            toolsContext: null,
            images: null,
            cancellationToken
        );

        var generationTime = DateTime.UtcNow - startTime;
        var htmlContent = response.Result ?? string.Empty;

        // Parse the generated HTML to extract article components
        var article = ParseGeneratedArticle(htmlContent, eventData);
        article.GenerationTimeMs = (int)generationTime.TotalMilliseconds;
        article.RawHtml = htmlContent;

        return article;
    }

    /// <summary>
    /// Validate article quality against standards.
    /// </summary>
    /// <param name="article">Article to validate</param>
    /// <returns>Validation result with quality metrics</returns>
    public ArticleValidation ValidateArticle(GeneratedArticle article)
    {
        var validation = new ArticleValidation
        {
            IsValid = true,
            Issues = new List<string>()
        };

        // Check HTML validity
        if (string.IsNullOrWhiteSpace(article.RawHtml))
        {
            validation.IsValid = false;
            validation.Issues.Add("Article HTML is empty");
        }

        // Check structure completeness
        if (string.IsNullOrWhiteSpace(article.Title))
        {
            validation.IsValid = false;
            validation.Issues.Add("Article missing title");
        }

        if (string.IsNullOrWhiteSpace(article.Summary))
        {
            validation.IsValid = false;
            validation.Issues.Add("Article missing summary");
        }

        if (article.Facts == null || article.Facts.Count == 0)
        {
            validation.IsValid = false;
            validation.Issues.Add("Article missing facts section");
        }

        // Check readability (Flesch-Kincaid score 60-70 = standard readability)
        var readabilityScore = CalculateFleschKincaidScore(article.Summary + " " + string.Join(" ", article.Narratives ?? new List<string>()));
        validation.ReadabilityScore = readabilityScore;

        if (readabilityScore < 50)
        {
            validation.Issues.Add($"Readability too difficult (score: {readabilityScore:F1}, target: 60-70)");
        }
        else if (readabilityScore > 80)
        {
            validation.Issues.Add($"Readability too simple (score: {readabilityScore:F1}, target: 60-70)");
        }

        // Check for bias language (basic check)
        var biasWords = new[] { "clearly", "obviously", "undoubtedly", "always", "never", "everyone knows" };
        var textToCheck = article.Summary + " " + string.Join(" ", article.Narratives ?? new List<string>());
        var foundBias = biasWords.Where(word => textToCheck.Contains(word, StringComparison.OrdinalIgnoreCase)).ToList();

        if (foundBias.Any())
        {
            validation.Issues.Add($"Potential bias language detected: {string.Join(", ", foundBias)}");
        }

        // Check length (articles should be substantive but not overwhelming)
        var wordCount = textToCheck.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        validation.WordCount = wordCount;

        if (wordCount < 200)
        {
            validation.Issues.Add($"Article too short ({wordCount} words, minimum: 200)");
        }
        else if (wordCount > 2000)
        {
            validation.Issues.Add($"Article too long ({wordCount} words, maximum: 2000)");
        }

        return validation;
    }

    private string BuildSystemPrompt()
    {
        return @"You are a professional journalist AI specialized in creating neutral, fact-based news articles.

Your role:
- Transform event data into readable, engaging articles
- Maintain strict neutrality and objectivity
- Use clear, accessible language (Flesch-Kincaid reading ease: 60-70)
- Structure articles with Title, Summary, Facts, Narratives, and Faction Analysis
- Generate valid HTML for WordPress publishing
- Focus on facts, not opinions
- Avoid bias words (clearly, obviously, undoubtedly, always, never)
- Write for general audience comprehension

Style guidelines:
- Professional but accessible tone
- Active voice preferred
- Short paragraphs (3-5 sentences)
- Use concrete examples
- Attribute all claims to sources
- Present multiple perspectives when applicable

Quality standards:
- Accuracy: All facts must be verifiable from provided data
- Clarity: Ideas expressed simply and directly
- Completeness: All major aspects covered
- Balance: Multiple viewpoints presented fairly
- Readability: Target 8th-10th grade reading level";
    }

    private string BuildGenerationPrompt(EventData eventData)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Generate a news article from this event data:");
        sb.AppendLine();
        sb.AppendLine($"Event Title: {eventData.Title}");
        sb.AppendLine($"Event Date: {eventData.Date:yyyy-MM-dd}");
        sb.AppendLine($"Event Type: {eventData.EventType}");
        sb.AppendLine();

        if (eventData.Facts != null && eventData.Facts.Any())
        {
            sb.AppendLine("Key Facts:");
            foreach (var fact in eventData.Facts)
            {
                sb.AppendLine($"  - {fact}");
            }
            sb.AppendLine();
        }

        if (eventData.Narratives != null && eventData.Narratives.Any())
        {
            sb.AppendLine("Narratives/Perspectives:");
            foreach (var narrative in eventData.Narratives)
            {
                sb.AppendLine($"  - {narrative}");
            }
            sb.AppendLine();
        }

        if (eventData.Factions != null && eventData.Factions.Any())
        {
            sb.AppendLine("Involved Factions/Parties:");
            foreach (var faction in eventData.Factions)
            {
                sb.AppendLine($"  - {faction.Name}: {faction.Position}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Generate an article with this HTML structure:");
        sb.AppendLine(@"
<article>
  <h1>[Article Title]</h1>

  <section class=""summary"">
    <p>[2-3 sentence summary capturing the essence]</p>
  </section>

  <section class=""facts"">
    <h2>What Happened</h2>
    <ul>
      <li>[Fact 1]</li>
      <li>[Fact 2]</li>
      <li>[Additional facts...]</li>
    </ul>
  </section>

  <section class=""narratives"">
    <h2>Perspectives</h2>
    <p>[Narrative paragraph 1]</p>
    <p>[Narrative paragraph 2]</p>
  </section>

  <section class=""factions"">
    <h2>Stakeholders</h2>
    <div class=""faction"">
      <h3>[Faction Name]</h3>
      <p>[Their position and interests]</p>
    </div>
  </section>
</article>");

        sb.AppendLine();
        sb.AppendLine("Requirements:");
        sb.AppendLine("- Title must be compelling but neutral");
        sb.AppendLine("- Summary must be 2-3 sentences, capturing the essence");
        sb.AppendLine("- Facts section must list verifiable facts from the data");
        sb.AppendLine("- Narratives section presents different interpretations");
        sb.AppendLine("- Factions section explains stakeholder positions");
        sb.AppendLine("- Use valid HTML with semantic tags");
        sb.AppendLine("- Maintain neutral, fact-based tone throughout");
        sb.AppendLine("- Target reading level: 8th-10th grade (Flesch-Kincaid 60-70)");

        return sb.ToString();
    }

    private GeneratedArticle ParseGeneratedArticle(string htmlContent, EventData sourceData)
    {
        // Basic HTML parsing (in production, use HtmlAgilityPack or similar)
        var article = new GeneratedArticle
        {
            RawHtml = htmlContent,
            EventDate = sourceData.Date,
            EventType = sourceData.EventType
        };

        // Extract title (simplified parsing)
        var titleMatch = System.Text.RegularExpressions.Regex.Match(htmlContent, @"<h1>(.*?)</h1>", System.Text.RegularExpressions.RegexOptions.Singleline);
        if (titleMatch.Success)
        {
            article.Title = StripHtmlTags(titleMatch.Groups[1].Value).Trim();
        }

        // Extract summary
        var summaryMatch = System.Text.RegularExpressions.Regex.Match(htmlContent, @"<section class=""summary"">(.*?)</section>", System.Text.RegularExpressions.RegexOptions.Singleline);
        if (summaryMatch.Success)
        {
            article.Summary = StripHtmlTags(summaryMatch.Groups[1].Value).Trim();
        }

        // Extract facts
        var factsMatch = System.Text.RegularExpressions.Regex.Match(htmlContent, @"<section class=""facts"">(.*?)</section>", System.Text.RegularExpressions.RegexOptions.Singleline);
        if (factsMatch.Success)
        {
            var factItems = System.Text.RegularExpressions.Regex.Matches(factsMatch.Groups[1].Value, @"<li>(.*?)</li>");
            article.Facts = factItems.Select(m => StripHtmlTags(m.Groups[1].Value).Trim()).ToList();
        }

        // Extract narratives
        var narrativesMatch = System.Text.RegularExpressions.Regex.Match(htmlContent, @"<section class=""narratives"">(.*?)</section>", System.Text.RegularExpressions.RegexOptions.Singleline);
        if (narrativesMatch.Success)
        {
            var paragraphs = System.Text.RegularExpressions.Regex.Matches(narrativesMatch.Groups[1].Value, @"<p>(.*?)</p>");
            article.Narratives = paragraphs.Where(m => !m.Value.Contains("<h2>")).Select(m => StripHtmlTags(m.Groups[1].Value).Trim()).ToList();
        }

        return article;
    }

    private string StripHtmlTags(string html)
    {
        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
    }

    private double CalculateFleschKincaidScore(string text)
    {
        // Simplified Flesch Reading Ease calculation
        // Formula: 206.835 - 1.015 * (words/sentences) - 84.6 * (syllables/words)
        // Score: 90-100 = very easy, 60-70 = standard, 0-30 = very difficult

        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0 || sentences.Length == 0)
            return 0;

        var totalSyllables = words.Sum(word => CountSyllables(word));

        var wordsPerSentence = (double)words.Length / sentences.Length;
        var syllablesPerWord = (double)totalSyllables / words.Length;

        var score = 206.835 - (1.015 * wordsPerSentence) - (84.6 * syllablesPerWord);

        return Math.Max(0, Math.Min(100, score)); // Clamp to 0-100
    }

    private int CountSyllables(string word)
    {
        // Simplified syllable counting
        word = word.ToLower().Trim();
        if (word.Length <= 3) return 1;

        var vowels = "aeiouy";
        var syllableCount = 0;
        var previousWasVowel = false;

        foreach (var c in word)
        {
            var isVowel = vowels.Contains(c);
            if (isVowel && !previousWasVowel)
            {
                syllableCount++;
            }
            previousWasVowel = isVowel;
        }

        // Adjust for silent 'e'
        if (word.EndsWith("e") && syllableCount > 1)
        {
            syllableCount--;
        }

        return Math.Max(1, syllableCount);
    }
}

/// <summary>
/// Input data for article generation.
/// </summary>
public class EventData
{
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string EventType { get; set; } = string.Empty;
    public List<string> Facts { get; set; } = new();
    public List<string> Narratives { get; set; } = new();
    public List<FactionInfo> Factions { get; set; } = new();
}

/// <summary>
/// Information about a faction/party involved in the event.
/// </summary>
public class FactionInfo
{
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
}

/// <summary>
/// Generated article output.
/// </summary>
public class GeneratedArticle
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> Facts { get; set; } = new();
    public List<string> Narratives { get; set; } = new();
    public string RawHtml { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int GenerationTimeMs { get; set; }
}

/// <summary>
/// Article validation result.
/// </summary>
public class ArticleValidation
{
    public bool IsValid { get; set; }
    public List<string> Issues { get; set; } = new();
    public double ReadabilityScore { get; set; }
    public int WordCount { get; set; }
}
