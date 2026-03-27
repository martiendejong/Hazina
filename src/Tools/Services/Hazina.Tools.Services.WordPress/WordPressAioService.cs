using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using HazinaStore.Models.WordPress;
using Hazina.Tools.Data;
using Microsoft.Extensions.Configuration;
using Hazina.Tools.Models;
using System.Threading;
using Hazina.LLMs;
using Microsoft.SemanticKernel.ChatCompletion;

namespace HazinaStore.Services
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
                return JsonSerializer.Deserialize<KnowledgeBaseResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
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

                return JsonSerializer.Deserialize<List<PageOverview>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<PageOverview>();
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
                return JsonSerializer.Deserialize<AioInformatieResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
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

            try
            {
                // 1. Load FAQ prompt template
                var promptTemplate = await LoadFaqPromptTemplate(project);

                // 2. Get related pages for cross-linking context
                var relatedPages = await GetRelatedPages(project, pageId, pageInfo);

                // 3. Build prompt with template variables
                var prompt = BuildPrompt(promptTemplate, pageInfo, relatedPages);

                // 4. Generate FAQs using AI
                var questions = await GenerateStructuredFaqs(project, prompt);

                // 5. Store FAQs in WordPress
                var data = new AioInformatieResponse
                {
                    PageId = pageId,
                    Questions = questions
                };

                await StoreAioInformatieAsync(data);

                Console.WriteLine($"[GenerateQuestions] Successfully generated {questions.Count} FAQs for page {pageId} ({pageInfo.Title})");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GenerateQuestions] Error generating FAQs for page {pageId}: {ex.Message}");

                // Fallback to simple stub if AI generation fails
                var fallbackQa = new AioInformationQuestion
                {
                    Question = $"Waar gaat de pagina '{pageInfo.Title}' over?",
                    Answer = (pageInfo.Content ?? string.Empty).Trim().Length > 0
                        ? ((pageInfo.Content ?? string.Empty).Length > 800 ? (pageInfo.Content ?? string.Empty).Substring(0, 800) + "..." : pageInfo.Content ?? string.Empty)
                        : "Geen inhoud gevonden op de pagina."
                };

                var fallbackData = new AioInformatieResponse
                {
                    PageId = pageId,
                    Questions = new List<AioInformationQuestion> { fallbackQa }
                };

                await StoreAioInformatieAsync(fallbackData);

                Console.WriteLine($"[GenerateQuestions] Used fallback stub for page {pageId} due to error");
            }
        }

        #region FAQ Generation Helper Methods

        /// <summary>
        /// Load FAQ generation prompt template from project store
        /// </summary>
        private async Task<FaqPromptTemplate> LoadFaqPromptTemplate(Project project)
        {
            // Determine store directory based on project ID
            // Convention: C:\stores\<project-id>\faq-generation.prompts.json
            var storeBasePath = @"C:\stores";
            var projectId = project.Id ?? "artrevisionist"; // Default to artrevisionist for now
            var promptPath = Path.Combine(storeBasePath, projectId, "faq-generation.prompts.json");

            if (!File.Exists(promptPath))
            {
                throw new FileNotFoundException($"FAQ prompt template not found at: {promptPath}");
            }

            var json = await File.ReadAllTextAsync(promptPath);
            var template = JsonSerializer.Deserialize<FaqPromptTemplate>(json);

            if (template == null || string.IsNullOrEmpty(template.System) || string.IsNullOrEmpty(template.UserTemplate))
            {
                throw new Exception("Invalid FAQ prompt template structure");
            }

            return template;
        }

        /// <summary>
        /// Get related pages for cross-linking context
        /// </summary>
        private async Task<List<RelatedPageInfo>> GetRelatedPages(Project project, int currentPageId, AioInformatieResponse currentPage)
        {
            try
            {
                var allPages = await GetPagesAsync();
                var related = new List<RelatedPageInfo>();

                // Filter out current page and get top 5 related
                var otherPages = allPages.Where(p => p.Id != currentPageId).Take(5);

                foreach (var page in otherPages)
                {
                    // Fetch full page info to get URL
                    try
                    {
                        var pageInfo = await GetAioInformatieAsync(page.Id);
                        if (pageInfo != null && !string.IsNullOrEmpty(pageInfo.Url))
                        {
                            related.Add(new RelatedPageInfo
                            {
                                Title = page.Title ?? "",
                                Url = pageInfo.Url
                            });
                        }
                    }
                    catch
                    {
                        // Skip pages that fail to load
                        continue;
                    }
                }

                return related;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GetRelatedPages] Error: {ex.Message}");
                return new List<RelatedPageInfo>(); // Return empty list on error
            }
        }

        /// <summary>
        /// Build prompt by injecting template variables
        /// </summary>
        private string BuildPrompt(FaqPromptTemplate template, AioInformatieResponse pageInfo, List<RelatedPageInfo> relatedPages)
        {
            // Format related pages as JSON for prompt
            var relatedPagesJson = JsonSerializer.Serialize(relatedPages.Select(rp => new
            {
                title = rp.Title,
                url = rp.Url
            }), new JsonSerializerOptions { WriteIndented = true });

            // Truncate content if too long (max ~6000 chars for context window)
            var content = pageInfo.Content ?? "";
            if (content.Length > 6000)
            {
                content = content.Substring(0, 6000) + "... (truncated)";
            }

            // Replace template variables
            var prompt = template.UserTemplate
                .Replace("{{title}}", pageInfo.Title ?? "")
                .Replace("{{url}}", pageInfo.Url ?? "")
                .Replace("{{content}}", content)
                .Replace("{{related_pages}}", relatedPagesJson);

            return prompt;
        }

        /// <summary>
        /// Generate structured FAQs using AI (SemanticKernel + GPT-4o)
        /// </summary>
        private async Task<List<AioInformationQuestion>> GenerateStructuredFaqs(Project project, string userPrompt)
        {
            // Load LLM configuration from project
            var llmClient = CreateLlmClient(project);

            // Load prompt template for system message
            var promptTemplate = await LoadFaqPromptTemplate(project);

            // Build messages
            var messages = new List<HazinaChatMessage>
            {
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.System,
                    Text = promptTemplate.System
                },
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.User,
                    Text = userPrompt
                }
            };

            // Call LLM with JSON response format
            var response = await llmClient.GetResponse(
                messages,
                HazinaChatResponseFormat.Json,
                toolsContext: null,
                images: null,
                cancel: CancellationToken.None
            );

            // Parse JSON response
            var jsonResponse = response.Result;

            // Deserialize to FAQ list
            var faqs = JsonSerializer.Deserialize<List<FaqEntry>>(jsonResponse);

            if (faqs == null || faqs.Count == 0)
            {
                throw new Exception("AI returned empty FAQ list");
            }

            // Convert to AioInformationQuestion format
            var questions = faqs.Select(faq => new AioInformationQuestion
            {
                Question = faq.Question,
                Answer = faq.Answer
            }).ToList();

            return questions;
        }

        /// <summary>
        /// Create LLM client for FAQ generation
        /// </summary>
        private ILLMClient CreateLlmClient(Project project)
        {
            // Use SemanticKernel with GPT-4o for cost-effective structured output
            var config = new Hazina.LLMs.SemanticKernelConfig
            {
                Provider = LLMProvider.OpenAI,
                Model = "gpt-4o", // Cost-effective, supports structured JSON output
                ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
                         throw new Exception("OPENAI_API_KEY environment variable not set"),
                Temperature = 0.7, // Balanced creativity/consistency
                MaxTokens = 2000,  // Sufficient for 5 Q&A pairs
                FallbackToSchemaInjection = true
            };

            return new Hazina.LLMs.SemanticKernelClientWrapper(config);
        }

        #endregion

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
                Answer = (info.Content ?? string.Empty).Length > 800 ? (info.Content ?? string.Empty).Substring(0, 800) + "..." : info.Content ?? string.Empty
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
            List<PageOverview>? pages = null;
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

    #region FAQ Generation Models

    /// <summary>
    /// Template structure for FAQ generation prompts
    /// </summary>
    internal class FaqPromptTemplate
    {
        public string System { get; set; } = string.Empty;
        public string UserTemplate { get; set; } = string.Empty;
    }

    /// <summary>
    /// Related page information for cross-linking context
    /// </summary>
    internal class RelatedPageInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>
    /// FAQ entry from AI response
    /// </summary>
    internal class FaqEntry
    {
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
    }

    #endregion
}
