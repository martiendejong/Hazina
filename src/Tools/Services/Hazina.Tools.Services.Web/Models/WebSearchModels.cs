using System.Collections.Generic;

namespace Hazina.Tools.Services.Web.Models
{
    /// <summary>
    /// Result of a web search operation.
    /// </summary>
    public class WebSearchResult
    {
        public bool Success { get; set; }
        public string? Query { get; set; }
        public int ResultCount { get; set; }
        public List<WebSearchResultItem> Results { get; set; } = new();
        public string? Source { get; set; } // "Bing" or "DuckDuckGo"
        public string? Error { get; set; }
    }

    /// <summary>
    /// A single web search result item.
    /// </summary>
    public class WebSearchResultItem
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Snippet { get; set; } = "";
    }

    /// <summary>
    /// Content fetched from a web page.
    /// </summary>
    public class WebPageContent
    {
        public bool Success { get; set; }
        public string Url { get; set; } = "";
        public string? Content { get; set; }
        public string? Error { get; set; }
    }
}
