using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

public class WebPageScraper
{
    public async static Task<string> ScrapeWebPage(string url, bool raw = false)
    {
        url = MakeValidUrl(url);
        using HttpClient client = new HttpClient();
        var response = await client.GetStringAsync(url);
        if (raw)
            return response;

        HtmlDocument htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(response);

        // Extracting text content (modify XPath as needed)
        var extractedText = htmlDoc.DocumentNode.SelectSingleNode("//body")?.InnerText.Trim() ?? "No content found";
        extractedText = RemoveExcessWhitespace(extractedText);

        return extractedText;
    }

    public static string MakeValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));

        // Check if URL already has a scheme
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "https://" + url; // Default to HTTPS
        }

        return url;
    }

    public static string RemoveExcessWhitespace(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Replace multiple spaces, tabs, and newlines with a single space
        return Regex.Replace(input.Trim(), @"\s+", " ");
    }
}