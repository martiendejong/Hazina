using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Headers;

namespace HazinaStore.Services
{
    public class WordpressBlogService : WordpressBaseService
    {
        public WordpressBlogService(IConfiguration configuration, string baseUrl, string username, string password, HttpClient httpClient = null) : base(configuration, baseUrl, username, password, httpClient)
        {
        }

        public async Task<(bool success, int postId, string url)> CreateBlogPostAsync(string title, string category, string text)
        {
            try
            {
                var url = $"{_baseUrl}/{_apiPrefix}/blog";
                var payload = new { titel = title, categorie = category, tekst = text };
                var jsonContent = JsonSerializer.Serialize(payload);
                using var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using var request = CreateHttpRequest(HttpMethod.Post, url, httpContent);
                var response = await SendWithRetryAsync(request, nameof(CreateBlogPostAsync));
                var resultString = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(resultString);
                    var root = doc.RootElement;
                    var postId = root.TryGetProperty("postId", out var pid) ? pid.GetInt32() : (root.TryGetProperty("id", out var altId) ? altId.GetInt32() : 0);
                    var link = root.TryGetProperty("url", out var uri) ? uri.GetString() : (root.TryGetProperty("link", out var altUri) ? altUri.GetString() : null);
                    return (true, postId, link);
                }
                else
                {
                    Console.Error.WriteLine($"[CreateBlogPostAsync] Error: {response.StatusCode}. Response: {resultString}");
                    return (false, 0, null);
                }
            }
            catch (Exception ex)
            {
                var msg = $"[CreateBlogPostAsync] Error creating blog post: {ex.Message}";
                Console.Error.WriteLine(msg);
                return (false, 0, null);
            }
        }

        public async Task<List<WordPressCategory>> GetCategoriesAsync()
        {
            var allCategories = new List<WordPressCategory>();
            int page = 1;
            const int perPage = 100;

            try
            {
                while (true)
                {
                    var url = $"{_baseUrl}/wp-json/wp/v2/categories?page={page}&per_page={perPage}";
                    using var request = CreateHttpRequest(HttpMethod.Get, url);
                    var response = await SendWithRetryAsync(request, nameof(GetCategoriesAsync));

                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var categories = JsonSerializer.Deserialize<List<WordPressCategory>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (categories == null || categories.Count == 0)
                        break;

                    allCategories.AddRange(categories);
                    if (categories.Count < perPage)
                        break;
                    page++;
                }

                return allCategories.OrderBy(c => c.Id).ToList();
            }
            catch (Exception ex)
            {
                var msg = $"[GetCategoriesAsync] Fout bij ophalen van categorieën. Exception: {ex.Message}";
                Console.Error.WriteLine(msg);
                throw new Exception(msg, ex);
            }
        }

        public async Task<WordPressCategory> CreateCategoryAsync(string name, string description = null, int? parent = null)
        {
            try
            {
                var url = $"{_baseUrl}/wp-json/wp/v2/categories";
                var payload = new Dictionary<string, object?>
                {
                    {"name", name },
                    {"description", description },
                    {"parent", parent }
                };
                var options = new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
                };
                var jsonContent = JsonSerializer.Serialize(payload, options);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
                using var request = CreateHttpRequest(HttpMethod.Post, url, httpContent);
                var response = await SendWithRetryAsync(request, nameof(CreateCategoryAsync));
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<WordPressCategory>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                var msg = $"[CreateCategoryAsync] Error creating category. Exception: {ex.Message}";
                Console.Error.WriteLine(msg);
                throw new Exception(msg, ex);
            }
        }

        public async Task<bool> DeleteCategoryAsync(int categoryId)
        {
            try
            {
                var url = $"{_baseUrl}/wp-json/wp/v2/categories/{categoryId}?force=true";
                using var request = CreateHttpRequest(HttpMethod.Delete, url);
                using var response = await SendWithRetryAsync(request, "DeleteCategoryAsync");
                if (response.IsSuccessStatusCode)
                    return true;
                var content = await response.Content.ReadAsStringAsync();
                Console.Error.WriteLine($"[DeleteCategoryAsync] Error: {response.StatusCode}. Response: {content}");
                return false;
            }
            catch (Exception ex)
            {
                var msg = $"[DeleteCategoryAsync] Error deleting category {categoryId}. Exception: {ex.Message}";
                Console.Error.WriteLine(msg);
                throw new Exception(msg, ex);
            }
        }

        public async Task<WordPressCategory> UpdateCategoryAsync(int id, string name, string description = null, int? parent = null)
        {
            try
            {
                var url = $"{_baseUrl}/wp-json/wp/v2/categories/{id}";
                var payload = new Dictionary<string, object?> { { "name", name } };
                if (!string.IsNullOrEmpty(description)) payload["description"] = description;
                if (parent.HasValue) payload["parent"] = parent;
                var jsonContent = JsonSerializer.Serialize(payload);
                using var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                using var request = CreateHttpRequest(HttpMethod.Put, url, httpContent);
                var response = await SendWithRetryAsync(request, nameof(UpdateCategoryAsync));
                var json = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
                return JsonSerializer.Deserialize<WordPressCategory>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                var msg = $"[UpdateCategoryAsync] Fout bij bijwerken van categorie. Exception: {ex.Message}";
                Console.Error.WriteLine(msg);
                throw new Exception(msg, ex);
            }
        }

        public async Task<bool> CategoryExistsAsync(int categoryId)
        {
            try
            {
                var url = $"{_baseUrl}/wp-json/wp/v2/categories/{categoryId}";
                using var request = CreateHttpRequest(HttpMethod.Get, url);
                var response = await SendWithRetryAsync(request, nameof(CategoryExistsAsync));
                if ((int)response.StatusCode == 404) return false;
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (HttpRequestException httpEx)
            {
                if (httpEx.Message.Contains("404")) return false;
                throw;
            }
            catch (Exception ex)
            {
                var msg = $"[CategoryExistsAsync] Fout bij opzoeken van categorie {categoryId}. Exception: {ex.Message}";
                Console.Error.WriteLine(msg);
                throw new Exception(msg, ex);
            }
        }
    }
}

