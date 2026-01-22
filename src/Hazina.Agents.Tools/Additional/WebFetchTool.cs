using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hazina.Agents.Tools.Additional;

/// <summary>
/// Tool for fetching web content
/// </summary>
public static class WebFetchTool
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static WebFetchTool()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "HazinaCoder/1.0 (Coding Assistant)");
    }

    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "web_fetch",
            description: "Fetch content from a URL. Strips HTML to return plain text.",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "url",
                    Type = "string",
                    Required = true,
                    Description = "URL to fetch"
                },
                new()
                {
                    Name = "method",
                    Type = "string",
                    Required = false,
                    Description = "HTTP method (GET, POST). Default: GET"
                },
                new()
                {
                    Name = "max_length",
                    Type = "number",
                    Required = false,
                    Description = "Maximum response length in characters (default: 50000)"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    if (!root.TryGetProperty("url", out var urlElement))
                        return "Error: url parameter is required";

                    var url = urlElement.GetString();
                    if (string.IsNullOrWhiteSpace(url))
                        return "Error: url cannot be empty";

                    var method = "GET";
                    if (root.TryGetProperty("method", out var methodElement))
                    {
                        method = methodElement.GetString()?.ToUpper() ?? "GET";
                    }

                    int maxLength = 50000;
                    if (root.TryGetProperty("max_length", out var maxLengthElement))
                    {
                        maxLength = maxLengthElement.GetInt32();
                    }

                    HttpResponseMessage response;
                    if (method == "POST")
                    {
                        response = await _httpClient.PostAsync(url, null, cancel);
                    }
                    else
                    {
                        response = await _httpClient.GetAsync(url, cancel);
                    }

                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancel);
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

                    var result = new StringBuilder();
                    result.AppendLine($"URL: {url}");
                    result.AppendLine($"Status: {(int)response.StatusCode} {response.StatusCode}");
                    result.AppendLine($"Content-Type: {contentType}");
                    result.AppendLine();

                    // Strip HTML if it's an HTML response
                    if (contentType.Contains("html"))
                    {
                        content = StripHtml(content);
                    }

                    // Truncate if too long
                    if (content.Length > maxLength)
                    {
                        content = content.Substring(0, maxLength) + "\n\n... (truncated)";
                    }

                    result.AppendLine("Content:");
                    result.AppendLine(content);

                    return result.ToString();
                }
                catch (HttpRequestException ex)
                {
                    return $"HTTP Error: {ex.Message}";
                }
                catch (TaskCanceledException)
                {
                    return "Error: Request timed out";
                }
                catch (Exception ex)
                {
                    return $"Error fetching URL: {ex.Message}";
                }
            }
        );
    }

    private static string StripHtml(string html)
    {
        // Remove script and style tags and their content
        html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);

        // Remove HTML tags
        html = Regex.Replace(html, @"<[^>]+>", " ");

        // Decode HTML entities
        html = System.Net.WebUtility.HtmlDecode(html);

        // Collapse whitespace
        html = Regex.Replace(html, @"\s+", " ");

        // Clean up multiple newlines
        html = Regex.Replace(html, @"(\r?\n){3,}", "\n\n");

        return html.Trim();
    }
}
