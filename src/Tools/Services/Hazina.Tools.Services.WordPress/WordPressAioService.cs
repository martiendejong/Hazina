using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using DevGPTStore.Models.WordPress;
using DevGPT.GenerationTools.Data;
using Microsoft.Extensions.Configuration;
using DevGPT.GenerationTools.Models;
using System.Threading;

namespace DevGPTStore.Services
{
    public class WordPressAioService : WordpressBaseService
    {
        public ProjectsRepository Projects;

        public WordPressAioService(IConfiguration configuration, ProjectsRepository projects, string baseUrl, string username, string password, HttpClient httpClient = null)
            : base(configuration, baseUrl, username, password, httpClient)
        {
            Projects = projects;
        }

        public async Task<KnowledgeBaseResponse> GetKennisbankAsync()
        {
            try
            {
                var url = $"{_baseUrl}/{_apiPrefix}/knowledgebase";
                using var request = CreateHttpRequest(HttpMethod.Get, url);
                using var response = await SendWithRetryAsync(request, nameof(GetKennisbankAsync));
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<KnowledgeBaseResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                var msg = $"[GetKennisbankAsync] Fout bij ophalen van kennisbank. Exception: {ex.Message}";
                Console.Error.WriteLine(msg);
                throw new Exception(msg, ex);
            }
        }

        public async Task<bool> StoreKennisbankAsync(KnowledgeBaseRequest data)
        {
            try
            {
                var url = $"{_baseUrl}/{_apiPrefix}/knowledgebase";
                var jsonContent = JsonSerializer.Serialize(data);
                using var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var req = CreateHttpRequest(HttpMethod.Post, url, httpContent);
                var response = await SendWithRetryAsync(req, nameof(StoreKennisbankAsync));
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                var msg = $"[StoreKennisbankAsync] Fout bij opslaan/updaten van kennisbank. Exception: {ex.Message}";
                Console.Error.WriteLine(msg);
                throw new Exception(msg, ex);
            }
        }

        public async Task<List<PageOverview>> GetPagesAsync()
        {
            try
            {
                var url = $"{_baseUrl}/{_apiPrefix}/pages";
                using var request = CreateHttpRequest(HttpMethod.Get, url);
                using var response = await SendWithRetryAsync(request, nameof(GetPagesAsync));
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();

                return JsonSerializer.Deserialize<List<PageOverview>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                var msg = $"[GetPagesAsync] Fout bij ophalen van WordPress paginas. Exception: {ex.Message}";
                Console.Error.WriteLine(msg);
                throw new Exception(msg, ex);
            }
        }

        public async Task<AioInformatieResponse> GetAioInformatieAsync(int pageId)
        {
            try
            {
                var url = $"{_baseUrl}/{_apiPrefix}/aio-information/{pageId}";
                using var request = CreateHttpRequest(HttpMethod.Get, url);
                using var response = await SendWithRetryAsync(request, nameof(GetAioInformatieAsync));
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<AioInformatieResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                var msg = $"[GetAioInformatieAsync] Fout bij ophalen van AIO info voor paginaId: {pageId}. Exception: {ex.Message}";
                Console.Error.WriteLine(msg);
                throw new Exception(msg, ex);
            }
        }

        public async Task ToggleAioAsync(Project project, ToggleRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var wpRequest = new WordpressToggleRequest(request);

            try
            {
                var url = $"{_baseUrl}/{_apiPrefix}/toggle";
                var jsonContent = JsonSerializer.Serialize(wpRequest);
                using var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var req = CreateHttpRequest(HttpMethod.Post, url, httpContent);
                var postResponse = await SendWithRetryAsync(req, nameof(StoreKennisbankAsync));
                postResponse.EnsureSuccessStatusCode();
                await Task.Delay(50);

                if (wpRequest.ToggleValue)
                {
                    var info = await GetAioInformatieAsync(request.PageId);
                    await Task.Delay(50);

                    if (info == null || !info.Questions.Any())
                    {
                        await GenerateQuestions(project, request.PageId);
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = $"[ToggleAioAsync] Fout bij togglen van AIO: {ex.Message}";
                Console.Error.WriteLine(msg);
                throw new Exception(msg, ex);
            }
        }

        public async Task GenerateQuestions(Project project, int pageId)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            var pageInfo = await GetAioInformatieAsync(pageId);
            if (pageInfo == null)
                throw new Exception("Kan pagina-informatie niet ophalen.");

            // Very simple fallback: generate a single QA based on the page title/content
            var qa = new AioInformationQuestion
            {
                Question = $"Waar gaat de pagina '{pageInfo.Title}' over?",
                Answer = (pageInfo.Content ?? string.Empty).Trim().Length > 0
                    ? (pageInfo.Content.Length > 800 ? pageInfo.Content.Substring(0, 800) + "..." : pageInfo.Content)
                    : "Geen inhoud gevonden op de pagina."
            };
            var data = new AioInformatieResponse
            {
                PageId = pageId,
                Questions = new List<AioInformationQuestion> { qa }
            };

            await StoreAioInformatieAsync(data);
        }

        // Overload with feedback parameter for backward compatibility
        public async Task GenerateQuestions(Project project, int pageId, string feedback)
        {
            await GenerateQuestions(project, pageId);
        }

        public async Task GenerateQuestion(Project project, int pageId, int index, string feedback)
        {
            // Fallback: fetch current info and rewrite a single QA entry at index
            var info = await GetAioInformatieAsync(pageId);
            if (info == null) return;
            if (info.Questions == null || info.Questions.Count == 0)
            {
                await GenerateQuestions(project, pageId);
                return;
            }
            var safeIndex = Math.Max(0, Math.Min(index, info.Questions.Count - 1));
            info.Questions[safeIndex] = new AioInformationQuestion
            {
                Question = $"Waar gaat de pagina '{info.Title}' over? (vraag {safeIndex + 1})",
                Answer = (info.Content ?? string.Empty).Length > 800 ? info.Content.Substring(0, 800) + "..." : info.Content
            };
            await StoreAioInformatieAsync(info);
        }

        public async Task<bool> StoreAioInformatieAsync(StoreAioInformatieRequest data)
        {
            try
            {
                var url = $"{_baseUrl}/{_apiPrefix}/store-aio-information";
                var jsonContent = JsonSerializer.Serialize(data);
                using var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var req = CreateHttpRequest(HttpMethod.Post, url, httpContent);
                var postResponse = await SendWithRetryAsync(req, nameof(StoreAioInformatieAsync));
                postResponse.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                var msg = $"[StoreAioInformatieAsync] Fout bij opslaan van AIO info: {ex.Message}";
                Console.Error.WriteLine(msg);
                throw new Exception(msg, ex);
            }
        }

        // Convenience overload for controller signature
        public Task<bool> StoreAioInformatieAsync(AioInformatieResponse data)
        {
            var payload = new
            {
                page_id = data.PageId,
                enabled = true,
                questions = data.Questions ?? new List<AioInformationQuestion>()
            };
            // Reuse existing method by serializing the anonymous payload through the HTTP layer
            // Serialize and send directly
            return StoreAioPayloadAsync(payload);
        }

        private async Task<bool> StoreAioPayloadAsync(object payload)
        {
            try
            {
                var url = $"{_baseUrl}/{_apiPrefix}/store-aio-information";
                var jsonContent = JsonSerializer.Serialize(payload);
                using var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var req = CreateHttpRequest(HttpMethod.Post, url, httpContent);
                var postResponse = await SendWithRetryAsync(req, nameof(StoreAioInformatieAsync));
                postResponse.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                var msg = $"[StoreAioInformatieAsync] Fout bij opslaan van AIO info: {ex.Message}";
                Console.Error.WriteLine(msg);
                throw new Exception(msg, ex);
            }
        }

        public async Task<List<string>> RegenerateQuestionsForAllEnabledPagesAsync(Project project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            List<PageOverview> pages = null;
            try
            {
                pages = await GetPagesAsync();
            }
            catch (Exception ex)
            {
                var msg = $"[RegenerateQuestionsForAllEnabledPagesAsync] Fout bij ophalen van pagina's. Exception: {ex.Message}";
                Console.Error.WriteLine(msg);
                throw new Exception(msg, ex);
            }
            if (pages == null)
                return new List<string>();

            var enabledPages = pages.Where(p => p != null && p.aio_enabled == true).ToList();
            var tries = 1;
            List<Tuple<string, PageOverview>> failedPages = new();
            while (tries < 5)
            {
                failedPages = await RegenerateQuestionsForPages(project, enabledPages, 100 * tries);
                enabledPages = failedPages.Select(f => f.Item2).ToList();
                await Task.Delay(tries * 100);
                tries++;
            }
            return failedPages.Select(p => p.Item1).ToList();
        }

        private async Task<List<Tuple<string, PageOverview>>> RegenerateQuestionsForPages(Project project, List<PageOverview> enabledPages, int delay)
        {
            var failedPages = new List<Tuple<string, PageOverview>>();
            foreach (var page in enabledPages)
            {
                try
                {
                    await GenerateQuestions(project, page.Id);
                }
                catch (Exception ex)
                {
                    failedPages.Add(new Tuple<string, PageOverview>(ex.Message, page));
                    var msg = $"[RegenerateQuestionsForAllEnabledPagesAsync] Fout bij opnieuw genereren van vragen voor paginaId: {page?.Id}. Exception: {ex.Message}";
                    Console.Error.WriteLine(msg);
                }
                await Task.Delay(delay);
            }
            return failedPages;
        }

        public async Task<List<string>> BulkToggleAioAsync(Project project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            var pages = await GetPagesAsync();
            await Task.Delay(300);

            if (pages == null || pages.Count == 0)
                return new List<string>();

            var enabledPages = pages.Where(p => p != null && p.aio_enabled == true).ToList();
            var enable = false;
            if (enabledPages.Count == 0)
            {
                enabledPages = pages.ToList();
                enable = true;
            }
            var tries = 1;
            List<Tuple<string, PageOverview>> failedPages = new();
            while (tries < 5)
            {
                failedPages = await TogglePages(project, enabledPages, enable, 300 * tries);
                enabledPages = failedPages.Select(f => f.Item2).ToList();
                await Task.Delay(tries * 300);
                tries++;
            }
            return failedPages.Select(p => p.Item1).ToList();
        }

        private async Task<List<Tuple<string, PageOverview>>> TogglePages(Project project, List<PageOverview> pages, bool enable, int delay)
        {
            var failedPages = new List<Tuple<string, PageOverview>>();
            foreach (var page in pages)
            {
                try
                {
                    var toggleRequest = new ToggleRequest
                    {
                        PageId = page.Id,
                        ToggleValue = enable
                    };
                    await ToggleAioAsync(project, toggleRequest);
                }
                catch (Exception ex)
                {
                    failedPages.Add(new Tuple<string, PageOverview>(ex.Message, page));
                    var msg = $"[RegenerateQuestionsForAllEnabledPagesAsync] Fout bij opnieuw genereren van vragen voor paginaId: {page?.Id}. Exception: {ex.Message}";
                    Console.Error.WriteLine(msg);
                }
                await Task.Delay(delay);
            }
            return failedPages;
        }

        // Minimal stubs for legacy endpoints
        public Task GenerateKennisbank(Project project)
        {
            // No-op fallback in refactor stage
            return Task.CompletedTask;
        }

        public Task GenerateKennisbankWithFeedback(Project project, string feedback)
        {
            return Task.CompletedTask;
        }

        public Task GenerateKennisbankCategoryWithFeedback(Project project, int categoryIndex, string feedback)
        {
            return Task.CompletedTask;
        }

        public Task GenerateKennisbankQuestionWithFeedback(Project project, int categoryIndex, int questionIndex, string feedback)
        {
            return Task.CompletedTask;
        }
    }
}
