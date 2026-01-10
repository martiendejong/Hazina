using System.Threading.Tasks;
using Hazina.Tools.Services.Web.Models;

namespace Hazina.Tools.Services.Web.Abstractions
{
    /// <summary>
    /// Interface for web search operations.
    /// </summary>
    public interface IWebSearchService
    {
        /// <summary>
        /// Search the web for information.
        /// Uses Bing API if configured, otherwise falls back to DuckDuckGo HTML scraping.
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="count">Number of results (1-10)</param>
        /// <returns>Search results</returns>
        Task<WebSearchResult> SearchAsync(string query, int count = 5);

        /// <summary>
        /// Fetch and read the content of a web page.
        /// </summary>
        /// <param name="url">URL to fetch</param>
        /// <param name="maxChars">Maximum characters to return (default: 15000)</param>
        /// <returns>Web page content</returns>
        Task<WebPageContent> FetchUrlAsync(string url, int maxChars = 15000);
    }
}
