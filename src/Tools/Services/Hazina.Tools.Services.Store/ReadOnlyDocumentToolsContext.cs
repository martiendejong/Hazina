using Hazina.Tools.Data;
using Hazina.Tools.Models;
using Hazina.Tools.Services.Web;
using Hazina.Tools.Services.BigQuery;
using Hazina.Tools.Services.FileOps;
using System.Text.Json;

namespace Hazina.Tools.Services.Store;

/// <summary>
/// Tools context providing read-only document and web access for chat conversations.
/// This context explicitly excludes data gathering tools to separate concerns between
/// conversational AI and background data extraction.
/// </summary>
/// <remarks>
/// Tools included:
/// - Web search and page reading
/// - Project file reading
/// - PDF analysis
/// - BigQuery queries
/// - Analysis field tools (if enabled)
///
/// Tools excluded:
/// - StoreGatheredData (handled by <see cref="DataGatheringToolsContext"/>)
/// </remarks>
public sealed class ReadOnlyDocumentToolsContext : ToolsContextBase
{
    private readonly WebScrapingService _webScrapingService;
    private readonly FileOperationsService _fileOperationsService;
    private readonly BigQueryService _bigQueryService;
    private readonly string _projectsFolder;

    /// <summary>
    /// The AI model being used for tool execution.
    /// </summary>
    public string Model { get; }

    /// <summary>
    /// The project identifier.
    /// </summary>
    public string ProjectId { get; }

    /// <summary>
    /// The chat identifier.
    /// </summary>
    public string ChatId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlyDocumentToolsContext"/> class.
    /// </summary>
    public ReadOnlyDocumentToolsContext(
        string model,
        string apiKey,
        IDocumentStore store,
        ProjectsRepository projects,
        IntakeRepository intake,
        string projectId,
        string chatId,
        object agent,
        string userId = "",
        IAnalysisFieldsProvider? analysisProvider = null,
        AnalysisToolsOptions? analysisOptions = null)
    {
        Model = model;
        ProjectId = projectId;
        ChatId = chatId;
        _projectsFolder = projects.ProjectsFolder;

        var fileLocator = new ProjectFileLocator(_projectsFolder);
        _webScrapingService = new WebScrapingService(apiKey, model, fileLocator, intake, projectId, agent);
        _fileOperationsService = new FileOperationsService(store, projects, projectId, chatId, userId, apiKey);
        _bigQueryService = new BigQueryService(apiKey) { Projects = projects, ProjectId = projectId };

        AddWebTools();
        AddFileTools();
        AddBigQueryTools();

        // Analysis tools are optional
        analysisOptions ??= new AnalysisToolsOptions { Enabled = true };
        if (analysisOptions.Enabled)
        {
            analysisProvider ??= new FileSystemAnalysisFieldsProvider(projects);
            AddAnalysisTools(analysisProvider, store);
        }
    }

    private void AddWebTools()
    {
        var queryParam = CreateParameter("query", "The search query", "string", required: true);
        var urlParam = CreateParameter("url", "The URL to read", "string", required: true);
        var rawParam = CreateParameter("raw", "Return raw HTML instead of extracted text", "boolean", required: false);

        Add("PerformWebSearch", "Performs a web search using the provided query.",
            [queryParam],
            async (messages, toolCall, cancel) =>
            {
                var query = GetStringParameter(toolCall, "query");
                return query != null
                    ? await _webScrapingService.PerformWebSearchAsync(query)
                    : "Invalid call, parameter 'query' was not provided.";
            });

        Add("PerformReadSitemap", "Reads the sitemap from a sitemap URL.",
            [urlParam],
            async (messages, toolCall, cancel) =>
            {
                var url = GetStringParameter(toolCall, "url");
                return url != null
                    ? await _webScrapingService.ReadSitemapAsync(url)
                    : "Invalid call, parameter 'url' was not provided.";
            });

        Add("PerformReadHtmlPage", "Reads an HTML page and returns the content.",
            [urlParam, rawParam],
            async (messages, toolCall, cancel) =>
            {
                var url = GetStringParameter(toolCall, "url");
                if (url == null) return "Invalid call, parameter 'url' was not provided.";

                var raw = GetBoolParameter(toolCall, "raw") ?? false;
                return await _webScrapingService.ReadWebPageAsync(url, raw);
            });
    }

    private void AddFileTools()
    {
        var fileParam = CreateParameter("file", "The file name or path", "string", required: true);

        Add("PerformReadProjectFile", "Reads and returns a file from the project.",
            [fileParam],
            async (messages, toolCall, cancel) =>
            {
                var file = GetStringParameter(toolCall, "file");
                return file != null
                    ? await _fileOperationsService.ReadProjectFileAsync(file)
                    : "Invalid call, parameter 'file' was not provided.";
            });

        Add("AnalyseChatPdfFile", "Analyze a PDF file that is included in the chat.",
            [fileParam],
            async (messages, toolCall, cancel) =>
            {
                var file = GetStringParameter(toolCall, "file");
                return file != null
                    ? await _fileOperationsService.AnalyzeChatPdfFileAsync(file)
                    : "Invalid call, parameter 'file' was not provided.";
            });

        Add("PerformGetProjectFilesList", "Gets the list of files available in the project.",
            [],
            async (messages, toolCall, cancel) => await _fileOperationsService.GetProjectFilesListAsync());

        var problemParam = CreateParameter("problem_statement", "The problem to reason about", "string", required: true);
        Add("PerformReasoning", "Performs reasoning based on the problem statement.",
            [problemParam],
            async (messages, toolCall, cancel) =>
            {
                var problem = GetStringParameter(toolCall, "problem_statement");
                return problem != null
                    ? await _fileOperationsService.PerformReasoningAsync(problem, messages)
                    : "Invalid call, parameter 'problem_statement' was not provided.";
            });
    }

    private void AddBigQueryTools()
    {
        var promptParam = CreateParameter("prompt", "The query prompt for BigQuery", "string", required: true);

        Add("PerformGetBigQueryResults", "Query Google BigQuery with a prompt.",
            [promptParam],
            async (messages, toolCall, cancel) =>
            {
                var prompt = GetStringParameter(toolCall, "prompt");
                return await _bigQueryService.PerformBigQueryResultsAsync(prompt ?? string.Empty, ProjectId);
            });
    }

    private void AddAnalysisTools(IAnalysisFieldsProvider analysisProvider, IDocumentStore store)
    {
        var keyParam = CreateParameter("key", "Analysis field key (e.g. 'topic-synopsis')", "string", required: true);
        var contentParam = CreateParameter("content", "The generated content for this field", "string", required: true);
        var feedbackParam = CreateParameter("feedback", "Optional feedback for refining generation", "string", required: false);

        Add("PerformGetAnalysisFieldValue", "Returns the value of an analysis field.",
            [keyParam],
            async (messages, toolCall, cancel) =>
            {
                var key = GetStringParameter(toolCall, "key");
                if (key == null) return "Invalid call, parameter 'key' is required.";

                // Read file content directly like the original StoreToolsContext implementation
                var path = Path.Combine(_projectsFolder, key + ".json");
                if (File.Exists(path))
                    return await File.ReadAllTextAsync(path, cancel);
                return string.Empty;
            });

        Add("PerformUpdateAnalysisField", "Updates content for an analysis field.",
            [keyParam, contentParam],
            async (messages, toolCall, cancel) =>
            {
                var key = GetStringParameter(toolCall, "key");
                var content = GetStringParameter(toolCall, "content");
                if (key == null || content == null)
                    return "Invalid call, parameters 'key' and 'content' are required.";

                return await SaveAnalysisFieldAsync(analysisProvider, store, key, content);
            });

        Add("PerformGenerateAnalysisFieldWithFeedback", "Stores generated content with optional feedback.",
            [keyParam, contentParam, feedbackParam],
            async (messages, toolCall, cancel) =>
            {
                var key = GetStringParameter(toolCall, "key");
                var content = GetStringParameter(toolCall, "content");
                var feedback = GetStringParameter(toolCall, "feedback");
                if (key == null || content == null)
                    return "Invalid call, parameters 'key' and 'content' are required.";

                return await SaveAnalysisFieldAsync(analysisProvider, store, key, content, feedback);
            });
    }

    private async Task<string> SaveAnalysisFieldAsync(
        IAnalysisFieldsProvider provider,
        IDocumentStore store,
        string key,
        string content,
        string? feedback = null)
    {
        var ok = await provider.SaveFieldAsync(ProjectId, key, content, feedback);
        if (!ok)
            return JsonSerializer.Serialize(new { ok = false, message = $"Unknown analysis key '{key}'." });

        try
        {
            var fields = await provider.GetFieldsAsync(ProjectId);
            var field = fields.FirstOrDefault(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (field != null)
            {
                await store.Embed(field.File);
                return JsonSerializer.Serialize(new { ok = true, key, file = field.File });
            }
        }
        catch
        {
            // Embedding failed but save succeeded
        }

        return JsonSerializer.Serialize(new { ok = true, key });
    }

    #region Parameter Helpers

    private static ChatToolParameter CreateParameter(string name, string description, string type, bool required)
        => new() { Name = name, Description = description, Type = type, Required = required };

    private static string? GetStringParameter(HazinaChatToolCall toolCall, string name)
    {
        using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
        return doc.RootElement.TryGetProperty(name, out var el) ? el.GetString() : null;
    }

    private static bool? GetBoolParameter(HazinaChatToolCall toolCall, string name)
    {
        using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
        return doc.RootElement.TryGetProperty(name, out var el) ? el.GetBoolean() : null;
    }

    #endregion

    /// <summary>
    /// Imports an entire website into the document store.
    /// </summary>
    public Task<bool> ReadWholeWebsite(string url)
        => _webScrapingService.ImportWholeWebsiteAsync(url);
}
