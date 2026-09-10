using Hazina.AI.FluentAPI.Configuration;
using Hazina.Examples.ArticleGenerator;

namespace Hazina.Examples.ArticleGeneratorDemo;

/// <summary>
/// Demonstrates the Article Generator agent for creating readable event articles.
///
/// This example shows:
/// - Generating articles from structured event data
/// - Using GPT-4 for professional content creation
/// - Structured template (Title, Summary, Facts, Narratives, Faction Analysis)
/// - HTML output for WordPress publishing
/// - Quality validation (readability, neutrality, completeness)
///
/// Prerequisites:
/// - .NET 8.0 or higher
/// - OpenAI API key (GPT-4 recommended for best quality)
///
/// Run: dotnet run
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Article Generator Agent Example ===\n");

        // 1. Setup: Get API key from environment
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("ERROR: OPENAI_API_KEY environment variable not set!");
            Console.WriteLine("\nSet it with:");
            Console.WriteLine("  export OPENAI_API_KEY=sk-your-key  # Linux/Mac");
            Console.WriteLine("  set OPENAI_API_KEY=sk-your-key     # Windows");
            return;
        }

        try
        {
            // 2. Create AI client (using GPT-4 for best article quality)
            var ai = QuickSetup.SetupOpenAI(apiKey);
            Console.WriteLine("✓ AI client initialized (GPT-4)\n");

            // 3. Create Article Generator agent
            var generator = new ArticleGenerator.ArticleGenerator(ai);
            Console.WriteLine("✓ Article Generator agent created\n");

            // 4. Example 1: Political event article
            Console.WriteLine("--- Example 1: Political Event ---\n");

            var politicalEvent = new EventData
            {
                Title = "Senate Passes Climate Legislation",
                Date = DateTime.Parse("2024-06-15"),
                EventType = "Politics",
                Facts = new List<string>
                {
                    "Senate voted 52-48 to pass comprehensive climate bill",
                    "Legislation allocates $400 billion over 10 years",
                    "Key provisions include renewable energy tax credits and carbon pricing",
                    "Bill now goes to House for consideration",
                    "Expected to reduce carbon emissions by 30% by 2035"
                },
                Narratives = new List<string>
                {
                    "Supporters argue this represents historic climate action and necessary investment in green economy",
                    "Critics claim the bill doesn't go far enough and excludes key environmental protections",
                    "Industry groups express concerns about economic impact on fossil fuel sector",
                    "Environmental organizations celebrate but note implementation challenges ahead"
                },
                Factions = new List<FactionInfo>
                {
                    new() { Name = "Democratic Senators", Position = "Strong support, highlighting job creation and climate urgency" },
                    new() { Name = "Republican Senators", Position = "Opposition, citing economic concerns and energy independence" },
                    new() { Name = "Environmental Groups", Position = "Cautious optimism, wanting stronger enforcement mechanisms" },
                    new() { Name = "Energy Industry", Position = "Mixed response, seeking transition support and clarity" }
                }
            };

            Console.WriteLine("Generating article for: " + politicalEvent.Title);
            var article1 = await generator.GenerateArticleAsync(politicalEvent);

            Console.WriteLine($"✓ Article generated in {article1.GenerationTimeMs}ms\n");

            // 5. Validate article quality
            Console.WriteLine("Validating article quality...");
            var validation1 = generator.ValidateArticle(article1);

            Console.WriteLine($"  Valid: {validation1.IsValid}");
            Console.WriteLine($"  Readability Score: {validation1.ReadabilityScore:F1} (target: 60-70)");
            Console.WriteLine($"  Word Count: {validation1.WordCount}");

            if (validation1.Issues.Any())
            {
                Console.WriteLine("  Issues:");
                foreach (var issue in validation1.Issues)
                {
                    Console.WriteLine($"    - {issue}");
                }
            }
            else
            {
                Console.WriteLine("  No issues found - article meets quality standards!");
            }

            Console.WriteLine();

            // 6. Display generated article
            Console.WriteLine("=== Generated Article ===\n");
            Console.WriteLine(article1.RawHtml);
            Console.WriteLine("\n" + new string('=', 80) + "\n");

            // 7. Example 2: Technology event article
            Console.WriteLine("--- Example 2: Technology Event ---\n");

            var techEvent = new EventData
            {
                Title = "Major AI Breakthrough in Medical Diagnostics",
                Date = DateTime.Parse("2024-06-20"),
                EventType = "Technology",
                Facts = new List<string>
                {
                    "Research team announces AI system with 95% accuracy in early cancer detection",
                    "System analyzes medical imaging 10x faster than human radiologists",
                    "Peer-reviewed study published in Nature Medicine with 50,000 patient dataset",
                    "FDA fast-track approval granted for clinical trials",
                    "Technology based on transformer architecture similar to GPT models"
                },
                Narratives = new List<string>
                {
                    "Medical community hails breakthrough as potential game-changer for early intervention",
                    "Patient advocates emphasize importance of accessibility and avoiding healthcare disparities",
                    "Privacy experts raise questions about medical data usage and patient consent",
                    "Insurance industry evaluates implications for coverage and cost structures"
                },
                Factions = new List<FactionInfo>
                {
                    new() { Name = "Research Team", Position = "Optimistic about clinical impact, emphasizing rigorous validation" },
                    new() { Name = "Medical Associations", Position = "Supportive but cautious, stressing need for physician oversight" },
                    new() { Name = "Patient Groups", Position = "Hopeful for improved outcomes, concerned about equitable access" },
                    new() { Name = "Tech Companies", Position = "Interested in partnerships and commercialization opportunities" }
                }
            };

            Console.WriteLine("Generating article for: " + techEvent.Title);
            var article2 = await generator.GenerateArticleAsync(techEvent);

            Console.WriteLine($"✓ Article generated in {article2.GenerationTimeMs}ms\n");

            var validation2 = generator.ValidateArticle(article2);
            Console.WriteLine($"  Valid: {validation2.IsValid}");
            Console.WriteLine($"  Readability Score: {validation2.ReadabilityScore:F1}");
            Console.WriteLine($"  Word Count: {validation2.WordCount}");

            if (!validation2.Issues.Any())
            {
                Console.WriteLine("  ✓ Article meets all quality standards\n");
            }

            Console.WriteLine("=== Generated Article ===\n");
            Console.WriteLine(article2.RawHtml);
            Console.WriteLine("\n" + new string('=', 80) + "\n");

            // 8. WordPress integration example
            Console.WriteLine("--- WordPress Integration Example ---\n");
            Console.WriteLine("To publish to WordPress:");
            Console.WriteLine("1. Copy the HTML from article.RawHtml");
            Console.WriteLine("2. Use WordPress REST API:");
            Console.WriteLine("   POST /wp/v2/posts");
            Console.WriteLine("   {");
            Console.WriteLine($"     \"title\": \"{article2.Title}\",");
            Console.WriteLine("     \"content\": \"[article.RawHtml]\",");
            Console.WriteLine("     \"status\": \"draft\"");
            Console.WriteLine("   }");
            Console.WriteLine("\n3. Review in WordPress editor before publishing");
            Console.WriteLine();

            // 9. Quality metrics summary
            Console.WriteLine("--- Quality Metrics Summary ---\n");
            Console.WriteLine("Article 1 (Politics):");
            Console.WriteLine($"  Title: {article1.Title}");
            Console.WriteLine($"  Facts: {article1.Facts.Count} listed");
            Console.WriteLine($"  Narratives: {article1.Narratives.Count} perspectives");
            Console.WriteLine($"  Readability: {validation1.ReadabilityScore:F1}/100");
            Console.WriteLine($"  Word Count: {validation1.WordCount}");
            Console.WriteLine($"  Generation Time: {article1.GenerationTimeMs}ms");
            Console.WriteLine();

            Console.WriteLine("Article 2 (Technology):");
            Console.WriteLine($"  Title: {article2.Title}");
            Console.WriteLine($"  Facts: {article2.Facts.Count} listed");
            Console.WriteLine($"  Narratives: {article2.Narratives.Count} perspectives");
            Console.WriteLine($"  Readability: {validation2.ReadabilityScore:F1}/100");
            Console.WriteLine($"  Word Count: {validation2.WordCount}");
            Console.WriteLine($"  Generation Time: {article2.GenerationTimeMs}ms");
            Console.WriteLine();

            Console.WriteLine(new string('=', 80));
            Console.WriteLine("✓ Success! Article Generator is working perfectly.");
            Console.WriteLine();

            Console.WriteLine("Key features demonstrated:");
            Console.WriteLine("  1. Structured event data → readable articles");
            Console.WriteLine("  2. GPT-4 powered professional content generation");
            Console.WriteLine("  3. Multiple perspectives and faction analysis");
            Console.WriteLine("  4. Quality validation (readability, neutrality, completeness)");
            Console.WriteLine("  5. HTML output ready for WordPress");
            Console.WriteLine("  6. Fact-based journalism approach");
            Console.WriteLine();

            Console.WriteLine("Next steps:");
            Console.WriteLine("  - Integrate with your event analysis pipeline");
            Console.WriteLine("  - Connect to WordPress REST API for automated publishing");
            Console.WriteLine("  - Add custom validation rules for your domain");
            Console.WriteLine("  - Experiment with different event types and data structures");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nERROR: {ex.Message}");
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
        }
    }
}
