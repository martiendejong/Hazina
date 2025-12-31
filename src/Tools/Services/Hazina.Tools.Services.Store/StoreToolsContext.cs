using Hazina.Tools.Data;
using Hazina.Tools.Models;
using Google.Cloud.BigQuery.V2;
using OpenAI.Chat;
using System.Text.Json;
using Hazina.Tools.Services.Web;
using Hazina.Tools.Services.BigQuery;
using Hazina.Tools.Services.FileOps;
using HtmlAgilityPack;
using System.Linq;

namespace Hazina.Tools.Services.Store
{
    public class StoreToolsContext : ToolsContextBase
    {
        public string Model { get; set; }
        public string ApiKey { get; set; }
        public IDocumentStore Store { get; set; }
        public ProjectsRepository Projects { get; set; }
        public IntakeRepository Intake { get; set; }
        public string ProjectId { get; set; }
        public string ChatId { get; set; }
        public string UserId { get; set; }
        public List<string> SelectedDocumentIds { get; set; } = new List<string>();

        private readonly WebScrapingService _webScrapingService;
        private readonly FileOperationsService _fileOperationsService;
        private readonly BigQueryService _bigQueryService;
        private const string AnalysisConfigFile = "analysis-fields.config.json";

        public StoreToolsContext(string model, string apiKey, IDocumentStore store, ProjectsRepository projects, IntakeRepository intake, string projectId, string chatId, object agent, string userId = "", IAnalysisFieldsProvider analysisProvider = null, AnalysisToolsOptions analysisOptions = null, List<string> selectedDocumentIds = null)
        {
            Model = model;
            ApiKey = apiKey;
            Store = store;
            Projects = projects;
            Intake = intake;
            ProjectId = projectId;
            ChatId = chatId;
            UserId = userId;
            SelectedDocumentIds = selectedDocumentIds ?? new List<string>();

            var fileLocator = new ProjectFileLocator(projects.ProjectsFolder);
            _webScrapingService = new WebScrapingService(apiKey, model, fileLocator, intake, projectId, agent);
            _fileOperationsService = new FileOperationsService(store, projects, projectId, chatId, userId, apiKey);
            _bigQueryService = new BigQueryService(apiKey) { Projects = projects, ProjectId = projectId };

            // add data gathering tools
            AddDataGatheringTools();



            analysisOptions ??= new AnalysisToolsOptions { Enabled = true };
            var enableAnalysisTools = analysisOptions.Enabled;
            if (analysisProvider == null)
            {
                // Provide a default provider if enabled and not supplied by the host
                analysisProvider = enableAnalysisTools ? new FileSystemAnalysisFieldsProvider(projects) : null;
            }

            if (enableAnalysisTools && analysisProvider != null)
            {
                AddAnalysisTools(analysisProvider, enableAnalysisTools);
            }

            var QueryParameter = new ChatToolParameter { Name = "query", Description = "The fields and epxressions that will be added in the SELECT", Type = "string", Required = true };
            var ProblemStatementParameter = new ChatToolParameter { Name = "problem_statement", Description = "The table that will be used in the FROM.", Type = "string", Required = true };
            var UrlParameter = new ChatToolParameter { Name = "url", Description = "The fields and epxressions that will be added in the WHERE. DO NOT USE THE ACCOUNT NAME OR ID TO FILTER, THAT WILL HAPPEN AUTOMATICALLY", Type = "string", Required = false };
            var RawParameter = new ChatToolParameter { Name = "raw", Description = "The fields and epxressions that will be added in the ORDER BY", Type = "boolean", Required = false };
            var FileParameter = new ChatToolParameter { Name = "file", Description = "The fields and epxressions that will be added in the GROUP BY", Type = "string", Required = false };
            var PromptParameter = new ChatToolParameter { Name = "prompt", Description = "The fields and epxressions that will be added in the HAVING", Type = "string", Required = false };

            var tool = new HazinaChatTool($"PerformWebSearch", $"Performs a web search using the provided query.",
                [QueryParameter],
                async (messages, toolCall, cancel) =>
                {
                    using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                    bool hasQuery = argumentsJson.RootElement.TryGetProperty("query", out JsonElement query);
                    if (hasQuery)
                    {
                        return await _webScrapingService.PerformWebSearchAsync(query.GetString());
                    }
                    return "Invalid call, parameter query was not provided.";
                });
            Tools.Add(tool);

            tool = new HazinaChatTool($"PerformReasoning", $"Performs reasoning based on the problem statement",
                [ProblemStatementParameter],
                async (messages, toolCall, cancel) =>
                {
                    using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                    bool hasQuery = argumentsJson.RootElement.TryGetProperty("problem_statement", out JsonElement query);
                    if (hasQuery)
                    {
                        return await _fileOperationsService.PerformReasoningAsync(query.GetString(), messages);
                    }
                    return "Invalid call, parameter problem_statement was not provided.";
                });
            Tools.Add(tool);

            tool = new HazinaChatTool($"PerformReadSitemap", $"Reads the sitemap from a sitemap url",
                [UrlParameter],
                async (messages, toolCall, cancel) =>
                {
                    using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                    bool hasQuery = argumentsJson.RootElement.TryGetProperty("url", out JsonElement query);
                    if (hasQuery)
                    {
                        return await _webScrapingService.ReadSitemapAsync(query.GetString());
                    }
                    return "Invalid call, parameter url was not provided.";
                });
            Tools.Add(tool);

            tool = new HazinaChatTool($"PerformReadHtmlPage", $"Reads an HTML page and returns the content, either raw HTML or the extracted text",
                [UrlParameter, RawParameter],
                async (messages, toolCall, cancel) =>
                {
                    using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                    bool hasQuery = argumentsJson.RootElement.TryGetProperty("url", out JsonElement query);
                    bool hasRaw = argumentsJson.RootElement.TryGetProperty("raw", out JsonElement queryRaw);
                    if (hasQuery)
                    {
                        var raw = hasRaw ? queryRaw.GetBoolean() : false;
                        return await _webScrapingService.ReadWebPageAsync(query.GetString(), raw);
                    }
                    return "Invalid call, parameter url was not provided.";
                });
            Tools.Add(tool);

            tool = new HazinaChatTool($"PerformReadProjectFile", $"Reads and returns a file from the project",
                [FileParameter],
                async (messages, toolCall, cancel) =>
                {
                    using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                    bool hasQuery = argumentsJson.RootElement.TryGetProperty("file", out JsonElement query);
                    if (hasQuery)
                    {
                        return await _fileOperationsService.ReadProjectFileAsync(query.GetString());
                    }
                    return "Invalid call, parameter file was not provided.";
                });
            Tools.Add(tool);

            tool = new HazinaChatTool($"AnalyseChatPdfFile", $"Analyse a PDF file that is included in the chat",
                [FileParameter],
                async (messages, toolCall, cancel) =>
                {
                    using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                    bool hasQuery = argumentsJson.RootElement.TryGetProperty("file", out JsonElement query);
                    if (hasQuery)
                    {
                        return await _fileOperationsService.AnalyzeChatPdfFileAsync(query.GetString());
                    }
                    return "Invalid call, parameter file was not provided.";
                });
            Tools.Add(tool);

            tool = new HazinaChatTool($"AnalyseChatDocument", $"Analyse any document file (PDF, DOCX, XLSX, etc.) that is included in the chat. Returns extracted text/summary from the document.",
                [FileParameter],
                async (messages, toolCall, cancel) =>
                {
                    using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                    bool hasQuery = argumentsJson.RootElement.TryGetProperty("file", out JsonElement query);
                    if (hasQuery)
                    {
                        return await _fileOperationsService.AnalyzeChatDocumentAsync(query.GetString());
                    }
                    return "Invalid call, parameter file was not provided.";
                });
            Tools.Add(tool);

            tool = new HazinaChatTool($"PerformGetProjectFilesList", $"Gets the list of files that are available in the project",
                [],
                async (messages, toolCall, cancel) =>
                {
                    return await _fileOperationsService.GetProjectFilesListAsync();
                });
            Tools.Add(tool);

            tool = new HazinaChatTool($"PerformGetBigQueryResults", $"Query the Google BigQuery MCP server with a prompt",
                [PromptParameter],
                async (messages, toolCall, cancel) =>
                {
                    using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                    bool found = argumentsJson.RootElement.TryGetProperty("prompt", out JsonElement prompt);
                    return await _bigQueryService.PerformBigQueryResultsAsync(prompt.ToString(), ProjectId);
                });
            Tools.Add(tool);
        }

        private void AddDataGatheringTools()
        {
            var KeyParameter = new ChatToolParameter { Name = "key", Description = "Gathered data field key (e.g. 'Brand Name', 'Company Website')", Type = "string", Required = true };
            var ContentParameter = new ChatToolParameter { Name = "data", Description = "The gathered information", Type = "string", Required = true };
            var tool = new HazinaChatTool($"StoreGatheredData", $"Store relevant information from the chat. Call this function to store information that the user provides through the chat.",
                [KeyParameter, ContentParameter],
                async (messages, toolCall, cancel) =>
                {
                    using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                    bool foundKey = argumentsJson.RootElement.TryGetProperty("key", out JsonElement key);
                    bool foundContent = argumentsJson.RootElement.TryGetProperty("data", out JsonElement content);
                    if (!foundKey)
                        return "ERROR: No key provided. try again with a key and data.";
                    if (!foundContent)
                        return "ERROR: No content provided. try again with a key and data.";
                    var keyFile = $"data.{key.ToString()}.txt";
                    var file = Path.Combine(Projects.ProjectsFolder, ProjectId, keyFile);
                    File.WriteAllText(file, content.ToString());
                    return $"File saved as {keyFile}";
                    // store file with key as name and content in it in the document store
                });
            Tools.Add(tool);
        }

        private void AddAnalysisTools(IAnalysisFieldsProvider analysisProvider, bool enableAnalysisTools)
        {
            var KeyParameter = new ChatToolParameter { Name = "key", Description = "Analysis field key (e.g. 'topic-synopsis', 'narrative-stance')", Type = "string", Required = true };
            var ContentParameter = new ChatToolParameter { Name = "content", Description = "The generated content for this analysis field", Type = "string", Required = true };
            var FeedbackParameter = new ChatToolParameter { Name = "feedback", Description = "Optional feedback used to refine generation before storing", Type = "string", Required = false };

            //var getAnalysisFields = new HazinaChatTool($"PerformGetAnalysisFields", $"Returns the list of analysis fields that can be generated for this project and their keys.",
            //    [],
            //    async (messages, toolCall, cancel) =>
            //    {
            //        var fields = await analysisProvider.GetFieldsAsync(ProjectId);
            //        var array = fields.Select(f => new { key = f.Key, file = f.File, name = f.DisplayName }).ToArray();
            //        return JsonSerializer.Serialize(array);
            //    });
            //Tools.Add(getAnalysisFields);

            var getAnalysisFieldValue = new HazinaChatTool($"PerformGetAnalysisFieldValue", $"Returns the value of the analysis field.",
                [KeyParameter],
                async (messages, toolCall, cancel) =>
                {
                    using var args = JsonDocument.Parse(toolCall.FunctionArguments);
                    if (!args.RootElement.TryGetProperty("key", out var keyEl))
                        return "Invalid call, parameters 'key' is required.";
                    var path = Path.Combine(Projects.ProjectsFolder, keyEl + ".json");
                    if (File.Exists(path))
                        return File.ReadAllText(path);
                    return "";
                });
            Tools.Add(getAnalysisFieldValue);

            var generateAnalysis = new HazinaChatTool($"PerformUpdateAnalysisField", $"Updates content for the given analysis field.",
            [KeyParameter, ContentParameter],
            async (messages, toolCall, cancel) =>
            {
                using var args = JsonDocument.Parse(toolCall.FunctionArguments);
                if (!args.RootElement.TryGetProperty("key", out var keyEl) || !args.RootElement.TryGetProperty("content", out var contentEl))
                    return "Invalid call, parameters 'key' and 'content' are required.";
                var key = keyEl.GetString() ?? string.Empty;
                var content = contentEl.GetString() ?? string.Empty;
                var ok = await analysisProvider.SaveFieldAsync(ProjectId, key, content);
                if (ok)
                {
                    try
                    {
                        var fields = await analysisProvider.GetFieldsAsync(ProjectId);
                        var relFile = fields.First(f => f.Key.Equals(key, System.StringComparison.OrdinalIgnoreCase)).File;
                        await Store.Embed(relFile);
                        return JsonSerializer.Serialize(new { ok = true, key, file = relFile });
                    }
                    catch { return JsonSerializer.Serialize(new { ok = true, key }); }
                }
                return JsonSerializer.Serialize(new { ok = false, message = $"Unknown analysis key '{key}'." });
            });
            Tools.Add(generateAnalysis);

            var generateAnalysisWithFeedback = new HazinaChatTool($"PerformGenerateAnalysisFieldWithFeedback", $"Stores generated content for the given analysis field, with an optional feedback parameter used during generation.",
            [KeyParameter, ContentParameter, FeedbackParameter],
            async (messages, toolCall, cancel) =>
            {
                using var args = JsonDocument.Parse(toolCall.FunctionArguments);
                if (!args.RootElement.TryGetProperty("key", out var keyEl) || !args.RootElement.TryGetProperty("content", out var contentEl))
                    return "Invalid call, parameters 'key' and 'content' are required.";
                var key = keyEl.GetString() ?? string.Empty;
                var content = contentEl.GetString() ?? string.Empty;
                var feedback = args.RootElement.TryGetProperty("feedback", out var fb) ? fb.GetString() : null;
                var ok = await analysisProvider.SaveFieldAsync(ProjectId, key, content, feedback);
                if (ok)
                {
                    try
                    {
                        var fields = await analysisProvider.GetFieldsAsync(ProjectId);
                        var relFile = fields.First(f => f.Key.Equals(key, System.StringComparison.OrdinalIgnoreCase)).File;
                        await Store.Embed(relFile);
                        return JsonSerializer.Serialize(new { ok = true, key, file = relFile });
                    }
                    catch { return JsonSerializer.Serialize(new { ok = true, key }); }
                }
                return JsonSerializer.Serialize(new { ok = false, message = $"Unknown analysis key '{key}'." });
            });
            Tools.Add(generateAnalysisWithFeedback);
        }

        // Note: analysis field map logic moved behind IAnalysisFieldsProvider for host-level customization

        public async Task<bool> ReadWholeWebsite(string url)
        {
            return await _webScrapingService.ImportWholeWebsiteAsync(url);
        }
    }
}
