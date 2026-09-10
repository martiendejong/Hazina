using Hazina.Agents.Tools.Agents;
using Hazina.Agents.Tools.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.Agents.Tools.Examples;

/// <summary>
/// Example demonstrating the SourceCollector agent
/// </summary>
public class SourceCollectorExample
{
    /// <summary>
    /// Demonstrates basic usage of the SourceCollector
    /// </summary>
    public static async Task RunExampleAsync()
    {
        // Create logger
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<SourceCollector>();

        // Create HTTP client with timeout
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Create source collector
        var collector = new SourceCollector(logger, httpClient);

        Console.WriteLine("Starting article collection from 50+ global RSS feeds...");
        Console.WriteLine();

        // Collect articles
        var startTime = DateTime.UtcNow;
        var articles = await collector.CollectArticlesAsync();
        var duration = DateTime.UtcNow - startTime;

        // Display statistics
        Console.WriteLine();
        Console.WriteLine("=== Collection Statistics ===");
        var stats = collector.GetCollectionStatistics(articles);
        foreach (var kvp in stats)
        {
            Console.WriteLine($"{kvp.Key}: {kvp.Value}");
        }

        Console.WriteLine($"Duration: {duration.TotalSeconds:F2} seconds");
        Console.WriteLine($"Target: <5 minutes (300 seconds)");
        Console.WriteLine($"Status: {(duration.TotalSeconds < 300 ? "✅ PASS" : "❌ FAIL")}");

        // Display sample articles by region
        Console.WriteLine();
        Console.WriteLine("=== Sample Articles by Region ===");

        var regions = articles.GroupBy(a => a.Region)
            .OrderByDescending(g => g.Count());

        foreach (var regionGroup in regions.Take(5))
        {
            Console.WriteLine();
            Console.WriteLine($"Region: {regionGroup.Key} ({regionGroup.Count()} articles)");

            foreach (var article in regionGroup.Take(3))
            {
                Console.WriteLine($"  - [{article.Publisher}] {article.Title}");
                Console.WriteLine($"    {article.Url}");
            }
        }
    }

    /// <summary>
    /// Demonstrates filtering articles by region
    /// </summary>
    public static async Task FilterByRegionExampleAsync(string targetRegion)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<SourceCollector>();
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        var collector = new SourceCollector(logger, httpClient);
        var allArticles = await collector.CollectArticlesAsync();

        var filteredArticles = allArticles
            .Where(a => a.Region.Equals(targetRegion, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Console.WriteLine($"Found {filteredArticles.Count} articles from {targetRegion}");

        foreach (var article in filteredArticles.Take(10))
        {
            Console.WriteLine($"[{article.Publisher}] {article.Title}");
        }
    }

    /// <summary>
    /// Demonstrates filtering articles by language
    /// </summary>
    public static async Task FilterByLanguageExampleAsync(string languageCode)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<SourceCollector>();
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        var collector = new SourceCollector(logger, httpClient);
        var allArticles = await collector.CollectArticlesAsync();

        var filteredArticles = allArticles
            .Where(a => a.Language.Equals(languageCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Console.WriteLine($"Found {filteredArticles.Count} articles in language '{languageCode}'");

        foreach (var article in filteredArticles.Take(10))
        {
            Console.WriteLine($"[{article.Publisher}] {article.Title}");
        }
    }
}
