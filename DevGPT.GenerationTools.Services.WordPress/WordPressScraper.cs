using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace DevGPT.GenerationTools.Services.WordPress
{
    public class WordPressScraper
    {
        private static async Task<T> CallClient<T>(string apiUrl, string username, string password, Func<HttpResponseMessage, Task<T>> handleResults, HttpClient? client = null)
        {
            if(client == null)
                client = GetAuthHttpClient(username, password);

            HttpResponseMessage response = await client.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                return await handleResults(response);
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");
                throw new Exception(response.Content.ToString());
            }
        }

        private static HttpClient GetAuthHttpClient(string username, string password)
        {
            HttpClient client = new HttpClient();
            if (username != null)
            {
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
            }

            return client;
        }

        public static async Task<Dictionary<string, WordPressPageData>> ScrapeWordpressCategories(string siteUrl, string username = null, string password = null, Func<string, string, string, string, bool> filterPages = null)
        {
            var handleResults = async (HttpResponseMessage response) =>
            {
                var items = new Dictionary<string, WordPressPageData>();

                string json = await response.Content.ReadAsStringAsync();
                JsonArray categories = (JsonArray)JsonNode.Parse(json);
                foreach (var category in categories)
                {
                    var id = category["id"].ToString();
                    var posts = await ScrapeWordpressPosts(siteUrl + "/wp-json/wp/v2/posts?categories=" + id, username, password, filterPages);
                    foreach(var pair in posts)
                    {
                        items[pair.Key] = pair.Value;
                    }
                }

                return items;
            };

            return await CallClient(siteUrl + "/wp-json/wp/v2/categories", username, password, handleResults);
        }

        public static async Task<Dictionary<string, WordPressPageData>> ScrapeWordpressPages(string apiUrl, string? username = null, string? password = null, Func<string, string, string, string, bool>? filterPages = null, HttpClient? client = null, bool getAll = true)
        {
            var numPages = 0;
            var handleResults = async (HttpResponseMessage response) =>
            {
                var items = new Dictionary<string, WordPressPageData>();

                string json = await response.Content.ReadAsStringAsync();

                var result = JsonNode.Parse(json);
                if (result is JsonArray array)
                {
                    JsonArray pages = (JsonArray)result;
                    numPages = pages.Count;
                    foreach (var page in pages)
                    {
                        ParseWpPageResult(filterPages, items, page);
                    }
                }
                else
                {
                    ParseWpPageResult(filterPages, items, result);
                }

                return items;
            };

            if (!getAll)
            {
                return await CallClient(apiUrl, username, password, handleResults, client);
            }

            apiUrl += "?per_page=100";
            var items = await CallClient(apiUrl, username, password, handleResults, client);
            var totalItems = items.ToDictionary();
            var page = 1;
            while (items.Count >= 100)
            {
                ++page;
                items = await CallClient(apiUrl + $"&page={page}", username, password, handleResults, client);
                foreach (var item in items)
                {
                    totalItems[item.Key] = item.Value;
                }                
            }
            return totalItems;
        }

        private static void ParseWpPageResult(Func<string, string, string, string, bool>? filterPages, Dictionary<string, WordPressPageData> items, JsonNode? page)
        {
            var title = page["title"]["rendered"].ToString();
            var link = page["link"].ToString();
            var content = "<html><body>" + page["content"]["rendered"].ToString() + "</body></html>";
            var html = WebPageScraper.ExtractTextFromHtml(content);
            if (filterPages == null || filterPages(link, title, html, content))
                items[link] = new WordPressPageData { Id = page["id"].ToString(), Name = link, Content = WebPageScraper.ExtractTextFromHtml(content) };
        }

        public static async Task<Dictionary<string, WordPressPageData>> ScrapeWordpressPosts(string apiUrl, string username = null, string password = null, Func<string, string, string, string, bool> filterPages = null)
        {
            var handleResults = async (HttpResponseMessage response) =>
            {
                var items = new Dictionary<string, WordPressPageData>();

                string json = await response.Content.ReadAsStringAsync();
                JsonArray pages = (JsonArray)JsonNode.Parse(json);
                foreach (var page in pages)
                {
                    var title = page["title"]["rendered"].ToString();
                    var link = page["link"].ToString();
                    var content = "<html><body>" + page["content"]["rendered"].ToString() + "</body></html>";
                    var html = WebPageScraper.ExtractTextFromHtml(content);
                    if (filterPages(link, title, html, content))
                        items[link] = new WordPressPageData { Id = page["id"].ToString(), Name = link, Content = WebPageScraper.ExtractTextFromHtml(content) };
                }

                return items;
            };

            return await CallClient(apiUrl, username, password, handleResults);
        }
    }
}
