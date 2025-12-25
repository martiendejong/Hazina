using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace DevGPT.GenerationTools.Services.WordPress
{
    public class WebPageScraper
    {
        public async static Task<string> ScrapeWebPage(string url, bool raw = false)
        {
            url = MakeValidUrl(url);
            using HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            var response = await client.GetStringAsync(url);
            if (raw)
                return response;

            string extractedText = ExtractTextFromHtml(response);

            return extractedText;
        }

        public static string ExtractTextFromHtml(string response)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response);

            var bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");

            if (bodyNode != null)
            {
                var texts = bodyNode
                    .Descendants()
                    .Select(n =>
                    {
                        if (n.NodeType == HtmlNodeType.Text && n.ParentNode.Name.ToLower() != "script" && n.ParentNode.Name.ToLower() != "style")
                        {
                            var text = RemoveExcessWhitespace(n.InnerText.Trim());
                            return string.IsNullOrEmpty(text) ? null : text;
                        }
                        else if (n.Name == "a")
                        {
                            var href = n.GetAttributeValue("href", "").Trim();
                            return string.IsNullOrEmpty(href) ? null : href;
                        }
                        return null;
                    })
                    .Where(s => !string.IsNullOrEmpty(s));

                var combinedText = string.Join("\n", texts);

                return combinedText;
            }
            else
            {
                return "No content found";
            }
        }

        public static string MakeValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be null or empty.", nameof(url));

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "https://" + url;
            }

            return url;
        }

        public static string RemoveExcessWhitespace(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            return Regex.Replace(input.Trim(), "\\s+", " ");
        }

        public static string StripNonAscii(string input)
        {
            return Regex.Replace(input, "[^a-zA-Z0-9]", "");
        }
    }
}
