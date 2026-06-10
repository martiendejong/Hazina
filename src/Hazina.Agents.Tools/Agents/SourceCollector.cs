using System.Security.Cryptography;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using Hazina.Agents.Tools.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.Agents.Tools.Agents;

/// <summary>
/// Agent that collects news articles from RSS feeds globally
/// </summary>
public class SourceCollector
{
    private readonly ILogger<SourceCollector> _logger;
    private readonly HttpClient _httpClient;
    private readonly HashSet<string> _collectedUrls;

    public SourceCollector(ILogger<SourceCollector> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _collectedUrls = new HashSet<string>();
    }

    /// <summary>
    /// Gets all configured RSS feed sources (50+ feeds)
    /// </summary>
    public List<RssFeedSource> GetRssFeedSources()
    {
        return new List<RssFeedSource>
        {
            // Western Sources
            new() { Publisher = "Reuters", FeedUrl = "https://www.reutersagency.com/feed/", Region = "Western", Language = "en" },
            new() { Publisher = "BBC", FeedUrl = "http://feeds.bbci.co.uk/news/rss.xml", Region = "Western", Language = "en" },
            new() { Publisher = "CNN", FeedUrl = "http://rss.cnn.com/rss/cnn_topstories.rss", Region = "Western", Language = "en" },
            new() { Publisher = "New York Times", FeedUrl = "https://rss.nytimes.com/services/xml/rss/nyt/World.xml", Region = "Western", Language = "en" },
            new() { Publisher = "The Guardian", FeedUrl = "https://www.theguardian.com/world/rss", Region = "Western", Language = "en" },
            new() { Publisher = "Washington Post", FeedUrl = "https://feeds.washingtonpost.com/rss/world", Region = "Western", Language = "en" },
            new() { Publisher = "Associated Press", FeedUrl = "https://apnews.com/apf-topnews", Region = "Western", Language = "en" },
            new() { Publisher = "Financial Times", FeedUrl = "https://www.ft.com/world?format=rss", Region = "Western", Language = "en" },
            new() { Publisher = "The Economist", FeedUrl = "https://www.economist.com/the-world-this-week/rss.xml", Region = "Western", Language = "en" },
            new() { Publisher = "Le Monde", FeedUrl = "https://www.lemonde.fr/rss/une.xml", Region = "Western", Language = "fr" },
            new() { Publisher = "Der Spiegel", FeedUrl = "https://www.spiegel.de/schlagzeilen/tops/index.rss", Region = "Western", Language = "de" },
            new() { Publisher = "El País", FeedUrl = "https://feeds.elpais.com/mrss-s/pages/ep/site/elpais.com/portada", Region = "Western", Language = "es" },

            // Russian Sources
            new() { Publisher = "RT", FeedUrl = "https://www.rt.com/rss/", Region = "Russian", Language = "en" },
            new() { Publisher = "TASS", FeedUrl = "https://tass.com/rss/v2.xml", Region = "Russian", Language = "en" },
            new() { Publisher = "RIA Novosti", FeedUrl = "https://ria.ru/export/rss2/archive/index.xml", Region = "Russian", Language = "ru" },
            new() { Publisher = "Sputnik", FeedUrl = "https://sputnikglobe.com/export/rss2/archive/index.xml", Region = "Russian", Language = "en" },
            new() { Publisher = "Interfax", FeedUrl = "https://www.interfax.ru/rss.asp", Region = "Russian", Language = "ru" },

            // Chinese Sources
            new() { Publisher = "Xinhua", FeedUrl = "http://www.xinhuanet.com/world/news_world.xml", Region = "Chinese", Language = "zh" },
            new() { Publisher = "Global Times", FeedUrl = "https://www.globaltimes.cn/rss/outbrain.xml", Region = "Chinese", Language = "en" },
            new() { Publisher = "CGTN", FeedUrl = "https://www.cgtn.com/rss/news.xml", Region = "Chinese", Language = "en" },
            new() { Publisher = "China Daily", FeedUrl = "http://www.chinadaily.com.cn/rss/world_rss.xml", Region = "Chinese", Language = "en" },
            new() { Publisher = "People's Daily", FeedUrl = "http://en.people.cn/rss/World.xml", Region = "Chinese", Language = "en" },

            // Middle East Sources
            new() { Publisher = "Al Jazeera", FeedUrl = "https://www.aljazeera.com/xml/rss/all.xml", Region = "Middle East", Language = "en" },
            new() { Publisher = "Al Arabiya", FeedUrl = "https://english.alarabiya.net/rss.xml", Region = "Middle East", Language = "en" },
            new() { Publisher = "Times of Israel", FeedUrl = "https://www.timesofisrael.com/feed/", Region = "Middle East", Language = "en" },
            new() { Publisher = "Haaretz", FeedUrl = "https://www.haaretz.com/cmlink/1.628075", Region = "Middle East", Language = "en" },
            new() { Publisher = "Middle East Eye", FeedUrl = "https://www.middleeasteye.net/rss", Region = "Middle East", Language = "en" },
            new() { Publisher = "The National UAE", FeedUrl = "https://www.thenationalnews.com/rss.xml", Region = "Middle East", Language = "en" },
            new() { Publisher = "Daily Sabah", FeedUrl = "https://www.dailysabah.com/rssFeed/11", Region = "Middle East", Language = "en" },

            // Africa Sources
            new() { Publisher = "Nation Africa", FeedUrl = "https://nation.africa/africa/rss", Region = "Africa", Language = "en" },
            new() { Publisher = "The Citizen Kenya", FeedUrl = "https://www.thecitizen.co.tz/tanzania/rss", Region = "Africa", Language = "en" },
            new() { Publisher = "News24", FeedUrl = "https://feeds.news24.com/articles/news24/TopStories/rss", Region = "Africa", Language = "en" },
            new() { Publisher = "Daily Maverick", FeedUrl = "https://www.dailymaverick.co.za/dmrss/", Region = "Africa", Language = "en" },
            new() { Publisher = "AllAfrica", FeedUrl = "https://allafrica.com/tools/headlines/rdf/latest/headlines.rdf", Region = "Africa", Language = "en" },
            new() { Publisher = "The East African", FeedUrl = "https://www.theeastafrican.co.ke/tea/rss", Region = "Africa", Language = "en" },

            // Latin America Sources
            new() { Publisher = "Telesur", FeedUrl = "https://www.telesurenglish.net/rss", Region = "Latin America", Language = "en" },
            new() { Publisher = "Prensa Latina", FeedUrl = "https://www.prensa-latina.cu/feed/", Region = "Latin America", Language = "es" },
            new() { Publisher = "Clarín", FeedUrl = "https://www.clarin.com/rss/lo-ultimo/", Region = "Latin America", Language = "es" },
            new() { Publisher = "Folha de S.Paulo", FeedUrl = "https://feeds.folha.uol.com.br/mundo/rss091.xml", Region = "Latin America", Language = "pt" },
            new() { Publisher = "El Universal", FeedUrl = "https://www.eluniversal.com.mx/rss.xml", Region = "Latin America", Language = "es" },
            new() { Publisher = "La Nación", FeedUrl = "https://www.lanacion.com.ar/arc/outboundfeeds/rss/", Region = "Latin America", Language = "es" },

            // Southeast Asia Sources
            new() { Publisher = "Channel NewsAsia", FeedUrl = "https://www.channelnewsasia.com/api/v1/rss-outbound-feed?_format=xml", Region = "Southeast Asia", Language = "en" },
            new() { Publisher = "Jakarta Post", FeedUrl = "https://www.thejakartapost.com/rss", Region = "Southeast Asia", Language = "en" },
            new() { Publisher = "Bangkok Post", FeedUrl = "https://www.bangkokpost.com/rss/data/news.xml", Region = "Southeast Asia", Language = "en" },
            new() { Publisher = "The Straits Times", FeedUrl = "https://www.straitstimes.com/news/singapore/rss.xml", Region = "Southeast Asia", Language = "en" },
            new() { Publisher = "Philippine Star", FeedUrl = "https://www.philstar.com/rss/headlines", Region = "Southeast Asia", Language = "en" },
            new() { Publisher = "VnExpress", FeedUrl = "https://vnexpress.net/rss/tin-moi-nhat.rss", Region = "Southeast Asia", Language = "vi" },

            // India/South Asia Sources
            new() { Publisher = "The Hindu", FeedUrl = "https://www.thehindu.com/news/international/feeder/default.rss", Region = "South Asia", Language = "en" },
            new() { Publisher = "Times of India", FeedUrl = "https://timesofindia.indiatimes.com/rssfeedstopstories.cms", Region = "South Asia", Language = "en" },
            new() { Publisher = "Dawn Pakistan", FeedUrl = "https://www.dawn.com/feeds/home", Region = "South Asia", Language = "en" },
            new() { Publisher = "Daily Star Bangladesh", FeedUrl = "https://www.thedailystar.net/frontpage/rss.xml", Region = "South Asia", Language = "en" },

            // East Asia Sources
            new() { Publisher = "Japan Times", FeedUrl = "https://www.japantimes.co.jp/feed/", Region = "East Asia", Language = "en" },
            new() { Publisher = "Korea Herald", FeedUrl = "http://www.koreaherald.com/common/rss_xml.php?ct=010000000000", Region = "East Asia", Language = "en" },
            new() { Publisher = "Yonhap News", FeedUrl = "https://en.yna.co.kr/RSS/news.xml", Region = "East Asia", Language = "en" },
        };
    }

    /// <summary>
    /// Collects articles from all RSS feeds
    /// </summary>
    public async Task<List<Article>> CollectArticlesAsync(CancellationToken cancellationToken = default)
    {
        var articles = new List<Article>();
        var sources = GetRssFeedSources().Where(s => s.IsActive).ToList();

        _logger.LogInformation("Starting collection from {Count} RSS feeds", sources.Count);
        var startTime = DateTime.UtcNow;

        var tasks = sources.Select(source => CollectFromFeedAsync(source, cancellationToken));
        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            articles.AddRange(result);
        }

        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation("Collection complete: {ArticleCount} articles from {SourceCount} feeds in {Duration:mm\\:ss}",
            articles.Count, sources.Count, duration);

        return articles;
    }

    /// <summary>
    /// Collects articles from a single RSS feed with error handling
    /// </summary>
    private async Task<List<Article>> CollectFromFeedAsync(RssFeedSource source, CancellationToken cancellationToken)
    {
        var articles = new List<Article>();

        try
        {
            _logger.LogDebug("Fetching RSS feed: {Publisher} ({FeedUrl})", source.Publisher, source.FeedUrl);

            using var response = await _httpClient.GetAsync(source.FeedUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings { Async = true });

            var feed = SyndicationFeed.Load(xmlReader);

            foreach (var item in feed.Items)
            {
                try
                {
                    var article = ParseArticle(item, source);

                    // Duplicate detection by URL
                    if (_collectedUrls.Add(article.UrlHash))
                    {
                        articles.Add(article);
                    }
                    else
                    {
                        _logger.LogDebug("Duplicate article skipped: {Url}", article.Url);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse article from {Publisher}", source.Publisher);
                }
            }

            _logger.LogDebug("Collected {Count} articles from {Publisher}", articles.Count, source.Publisher);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch RSS feed {Publisher} ({Url}): Network error",
                source.Publisher, source.FeedUrl);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout fetching RSS feed {Publisher} ({Url})",
                source.Publisher, source.FeedUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error collecting from {Publisher} ({Url})",
                source.Publisher, source.FeedUrl);
        }

        return articles;
    }

    /// <summary>
    /// Parses a syndication item into an Article
    /// </summary>
    private Article ParseArticle(SyndicationItem item, RssFeedSource source)
    {
        var url = item.Links.FirstOrDefault()?.Uri.ToString() ?? string.Empty;

        var article = new Article
        {
            Title = item.Title?.Text ?? string.Empty,
            Content = item.Summary?.Text ?? string.Empty,
            Url = url,
            UrlHash = ComputeHash(url),
            Publisher = source.Publisher,
            Region = source.Region,
            Language = source.Language,
            PublishTime = item.PublishDate.UtcDateTime,
            CollectedAt = DateTime.UtcNow,
            Author = item.Authors.FirstOrDefault()?.Name,
            Categories = item.Categories.Select(c => c.Name).ToList()
        };

        return article;
    }

    /// <summary>
    /// Computes SHA256 hash of a URL for duplicate detection
    /// </summary>
    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Gets statistics about collected articles
    /// </summary>
    public Dictionary<string, int> GetCollectionStatistics(List<Article> articles)
    {
        return new Dictionary<string, int>
        {
            ["TotalArticles"] = articles.Count,
            ["UniquePublishers"] = articles.Select(a => a.Publisher).Distinct().Count(),
            ["UniqueRegions"] = articles.Select(a => a.Region).Distinct().Count(),
            ["UniqueLanguages"] = articles.Select(a => a.Language).Distinct().Count(),
            ["DuplicatesSkipped"] = _collectedUrls.Count - articles.Count,
        };
    }
}
