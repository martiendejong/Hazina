using HazinaStore.Models;
using Hazina.Tools.Data;
using Hazina.Tools.Models;
using Hazina.Tools.Models.WordPress.Blogs;
using Hazina.LLMs;
using Hazina.LLMs.OpenAI;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hazina.Tools.Services.Store;

namespace HazinaStore.IntakeRegenerators
{
    public class ContentHooksRegenerator
    {
        private readonly ProjectsRepository _projects;
        private readonly ProjectFileLocator _fileLocator;
        private readonly IConfiguration _appConfig;
        private readonly HazinaStoreConfig _storeConfig;
        private readonly IAnalysisFieldsProvider _analysisFieldsProvider;

        public ContentHooksRegenerator(
            ProjectsRepository projects,
            IConfiguration appConfig,
            HazinaStoreConfig storeConfig,
            IAnalysisFieldsProvider analysisFieldsProvider)
        {
            _projects = projects;
            _fileLocator = new ProjectFileLocator(projects.ProjectsFolder);
            _appConfig = appConfig;
            _storeConfig = storeConfig;
            _analysisFieldsProvider = analysisFieldsProvider ?? throw new ArgumentNullException(nameof(analysisFieldsProvider));
        }

        public void CreateEmptyContentHooksFile(Project project)
        {
            var filePath = _fileLocator.GetPath(project.Id, ProjectFileLocator.ContentHooksFile);
            File.WriteAllText(filePath, "{\n  \"ContentHooks\": []\n}");
        }

        public async Task RegenerateAll(Project project, Action<string> log, object retry, CancellationToken cancel)
        {
            log?.Invoke("Starting content hooks generation...");

            try
            {
                // Build project context from analysis fields
                var context = await BuildProjectContextAsync(project.Id, log);

                if (string.IsNullOrWhiteSpace(context) || context.Length < 100)
                {
                    log?.Invoke($"Insufficient project context ({context?.Length ?? 0} chars). Creating empty file.");
                    CreateEmptyContentHooksFile(project);
                    return;
                }

                log?.Invoke($"Built project context ({context.Length} chars)");

                // Create prompt for content hooks generation
                var prompt = $@"Based on the following project context, generate 5-8 strategic content hooks (themes) that would be valuable for content marketing.

Content hooks are high-level themes or topics that resonate with the target audience and align with the brand's expertise. Each hook should:
- Be distinct and cover different aspects of the business
- Have clear value for the target audience
- Be specific enough to guide content creation but broad enough to generate multiple pieces
- Include 2-3 concrete examples of content that could be created

PROJECT CONTEXT:
{context}

Return ONLY JSON in this exact format (no markdown, no code blocks, no explanation):
{{""contentHooks"":[{{""id"":""unique-id"",""name"":""Hook Name"",""description"":""Brief description"",""reason"":""Why this matters for the brand"",""examples"":[""Example 1"",""Example 2""]}}]}}";

                log?.Invoke("Calling AI to generate content hooks...");

                // Call OpenAI to generate
                var apiKey = _storeConfig?.ApiSettings?.OpenApiKey;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    log?.Invoke("ERROR: No API key configured");
                    CreateEmptyContentHooksFile(project);
                    return;
                }

                var config = new OpenAIConfig(apiKey) { Model = "gpt-4o-mini" };
                ILLMClient client = new OpenAIClientWrapper(config);

                var messages = new List<HazinaChatMessage>
                {
                    new HazinaChatMessage { Role = HazinaMessageRole.System, Text = "You are a content strategy expert who generates strategic content themes." },
                    new HazinaChatMessage { Role = HazinaMessageRole.User, Text = prompt }
                };

                var llmResponse = await client.GetResponse(
                    messages,
                    HazinaChatResponseFormat.Text,
                    null,
                    null,
                    cancel);

                var response = llmResponse?.Result;
                log?.Invoke($"AI response received ({response?.Length ?? 0} chars)");

                if (string.IsNullOrWhiteSpace(response))
                {
                    log?.Invoke("ERROR: AI returned empty response");
                    CreateEmptyContentHooksFile(project);
                    return;
                }

                // Parse JSON response
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}') + 1;
                if (jsonStart < 0 || jsonEnd <= jsonStart)
                {
                    log?.Invoke($"ERROR: Could not find JSON in response");
                    CreateEmptyContentHooksFile(project);
                    return;
                }

                var json = response.Substring(jsonStart, jsonEnd - jsonStart);
                log?.Invoke("Parsing content hooks...");

                var result = JsonSerializer.Deserialize<ContentHooksResult>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.ContentHooks == null || result.ContentHooks.Count == 0)
                {
                    log?.Invoke("ERROR: No content hooks found in AI response");
                    CreateEmptyContentHooksFile(project);
                    return;
                }

                log?.Invoke($"Generated {result.ContentHooks.Count} content hooks");

                // Ensure IDs are set
                foreach (var hook in result.ContentHooks)
                {
                    if (string.IsNullOrWhiteSpace(hook.Id))
                    {
                        hook.Id = Guid.NewGuid().ToString("N");
                    }
                }

                // Save to file
                var wrapper = new ContentHooksFinal
                {
                    ContentHooks = result.ContentHooks.Select(h => new ContentHookFinal
                    {
                        Id = h.Id,
                        Name = h.Name ?? "Untitled",
                        Description = h.Description ?? "",
                        Reason = h.Reason ?? "",
                        Examples = h.Examples?.ToList()
                    }).ToList()
                };

                var filePath = _fileLocator.GetPath(project.Id, ProjectFileLocator.ContentHooksFile);
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var jsonOutput = JsonSerializer.Serialize(wrapper, jsonOptions);
                await File.WriteAllTextAsync(filePath, jsonOutput, cancel);

                log?.Invoke($"✓ Content hooks saved successfully ({wrapper.ContentHooks.Count} hooks)");
            }
            catch (Exception ex)
            {
                log?.Invoke($"ERROR: {ex.Message}");
                CreateEmptyContentHooksFile(project);
            }
        }

        /// <summary>
        /// Builds project context from analysis fields using IAnalysisFieldsProvider.
        /// Loads all configured analysis fields (brand profile, tone of voice, etc.) and extracts their content.
        /// </summary>
        private async Task<string> BuildProjectContextAsync(string projectId, Action<string> log)
        {
            var context = new StringBuilder();
            var projectPath = _fileLocator.GetProjectFolder(projectId);

            try
            {
                // Load basic project info
                var project = _projects.Load(projectId);
                if (project != null)
                {
                    context.AppendLine($"PROJECT: {project.Name}");
                    if (!string.IsNullOrWhiteSpace(project.Description))
                    {
                        context.AppendLine($"DESCRIPTION: {project.Description}");
                    }
                    if (!string.IsNullOrWhiteSpace(project.Website))
                    {
                        context.AppendLine($"WEBSITE: {project.Website}");
                    }
                    context.AppendLine();
                }

                // Load all analysis fields for this project using IAnalysisFieldsProvider
                var fields = await _analysisFieldsProvider.GetFieldsAsync(projectId);
                if (fields != null && fields.Count > 0)
                {
                    context.AppendLine("BRAND ANALYSIS:");

                    var loadedCount = 0;
                    foreach (var field in fields.Take(15)) // Limit to first 15 fields to avoid token overflow
                    {
                        if (string.IsNullOrWhiteSpace(field.File))
                            continue;

                        try
                        {
                            var filePath = Path.Combine(projectPath, field.File);
                            if (!File.Exists(filePath))
                                continue;

                            var content = await File.ReadAllTextAsync(filePath);
                            if (string.IsNullOrWhiteSpace(content))
                                continue;

                            // Try to extract text from JSON if it's a JSON file
                            string textContent = content;
                            if (field.File.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    // Most analysis fields store their content as plain JSON string
                                    // Example: "brand-profile.json" contains: "an ai branding tool biedt..."
                                    using var doc = JsonDocument.Parse(content);
                                    var root = doc.RootElement;

                                    // If it's a simple string value, use it directly
                                    if (root.ValueKind == JsonValueKind.String)
                                    {
                                        textContent = root.GetString() ?? content;
                                    }
                                    // For complex objects, use the raw JSON
                                    else
                                    {
                                        textContent = content;
                                    }
                                }
                                catch
                                {
                                    // If JSON parsing fails, just use the raw content
                                    textContent = content;
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(textContent))
                            {
                                var truncated = textContent.Length > 800 ? textContent.Substring(0, 800) + "..." : textContent;
                                var displayName = field.DisplayName ?? field.Key.Replace("-", " ").Replace("_", " ");
                                context.AppendLine($"{displayName.ToUpper()}: {truncated}");
                                context.AppendLine();
                                loadedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            log?.Invoke($"Warning: Could not load field '{field.Key}': {ex.Message}");
                            continue;
                        }
                    }

                    log?.Invoke($"Loaded {loadedCount} analysis fields");
                }

                log?.Invoke($"Loaded project context: {context.Length} chars");
            }
            catch (Exception ex)
            {
                log?.Invoke($"Warning: Error building context: {ex.Message}");
            }

            return context.ToString();
        }

        // Helper classes for JSON parsing
        private class ContentHooksResult
        {
            public List<ContentHookItem> ContentHooks { get; set; } = new();
        }

        private class ContentHookItem
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string Reason { get; set; } = "";
            public List<string> Examples { get; set; } = new();
        }
    }
}
