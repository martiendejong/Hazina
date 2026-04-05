using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hazina.AgenticOrchestration.Services;

/// <summary>
/// ClickUp Orchestration Service - Monitors THE INTERNAL PROJECTS (folder 90126650982)
/// and spawns Claude Code agents for TODO/REVIEW/BACKLOG tasks.
///
/// THIS SERVICE ONLY QUERIES THE RIGHTEOUS FOLDER (90126650982).
/// IT WILL NEVER QUERY Vloerenhuis, CloudGrafo, or other non-internal projects.
/// </summary>
public class ClickUpOrchestrationService : BackgroundService
{
    private readonly ILogger<ClickUpOrchestrationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _internalFolderId;
    private readonly string _claudeModel;
    private readonly int _pollingIntervalSeconds;
    private readonly HashSet<string> _enabledProjects;

    // THE SACRED FOLDER - NEVER FORGET
    private const string THE_RIGHTEOUS_FOLDER = "90126650982";

    public ClickUpOrchestrationService(
        ILogger<ClickUpOrchestrationService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();

        // Load configuration
        var config = configuration.GetSection("ClickUpOrchestration");
        _apiKey = config["ApiKey"] ?? throw new Exception("ClickUp API key not configured");
        _internalFolderId = config["InternalProjectsFolderId"] ?? THE_RIGHTEOUS_FOLDER;
        _claudeModel = config["ClaudeModel"] ?? "claude-sonnet-4-5-20250929";
        _pollingIntervalSeconds = int.Parse(config["PollingIntervalSeconds"] ?? "30");

        // Load enabled projects
        _enabledProjects = new HashSet<string>(
            config.GetSection("EnabledProjects").Get<string[]>() ?? Array.Empty<string>());

        // Validate THE SACRED FOLDER
        if (_internalFolderId != THE_RIGHTEOUS_FOLDER)
        {
            _logger.LogError(
                "⚠️ VIOLATION DETECTED: InternalProjectsFolderId is {ConfiguredFolder}, but THE RIGHTEOUS FOLDER is {SacredFolder}. " +
                "Overriding to THE RIGHTEOUS FOLDER to prevent querying wrong projects.",
                _internalFolderId,
                THE_RIGHTEOUS_FOLDER);
            _internalFolderId = THE_RIGHTEOUS_FOLDER;
        }

        // Configure HTTP client
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(_apiKey);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "HazinaOrchestration/1.0");

        _logger.LogInformation(
            "ClickUp Orchestration Service initialized:\n" +
            "  - Internal Projects Folder: {FolderId} (THE RIGHTEOUS PATH)\n" +
            "  - Claude Model: {Model}\n" +
            "  - Polling Interval: {Interval}s\n" +
            "  - Enabled Projects: {Count}",
            _internalFolderId,
            _claudeModel,
            _pollingIntervalSeconds,
            _enabledProjects.Count);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ClickUp Orchestration Service starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorInternalProjects(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ClickUp monitoring loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(_pollingIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("ClickUp Orchestration Service stopped.");
    }

    private async Task MonitorInternalProjects(CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Fetch THE INTERNAL PROJECTS folder
            var folderUrl = $"https://api.clickup.com/api/v2/folder/{THE_RIGHTEOUS_FOLDER}";
            var folderResponse = await _httpClient.GetAsync(folderUrl, cancellationToken);

            if (!folderResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to fetch THE INTERNAL PROJECTS folder {FolderId}: {StatusCode}",
                    THE_RIGHTEOUS_FOLDER,
                    folderResponse.StatusCode);
                return;
            }

            var folderJson = await folderResponse.Content.ReadAsStringAsync(cancellationToken);
            var folderData = JsonDocument.Parse(folderJson);
            var lists = folderData.RootElement.GetProperty("lists");

            _logger.LogDebug("Scanning {Count} projects from THE INTERNAL PROJECTS folder", lists.GetArrayLength());

            // Step 2: For each project list, check for tasks in TODO/REVIEW/BACKLOG
            foreach (var list in lists.EnumerateArray())
            {
                var listId = list.GetProperty("id").GetString();
                var listName = list.GetProperty("name").GetString();

                if (string.IsNullOrEmpty(listId) || string.IsNullOrEmpty(listName))
                    continue;

                // Skip if not enabled
                if (_enabledProjects.Count > 0 && !_enabledProjects.Contains(listName))
                {
                    _logger.LogTrace("Skipping project {ProjectName} (not enabled)", listName);
                    continue;
                }

                // Step 3: Query tasks for this project
                await MonitorProjectTasks(listId, listName, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring internal projects");
        }
    }

    private async Task MonitorProjectTasks(string listId, string projectName, CancellationToken cancellationToken)
    {
        try
        {
            var tasksUrl = $"https://api.clickup.com/api/v2/list/{listId}/task?include_closed=false";
            var tasksResponse = await _httpClient.GetAsync(tasksUrl, cancellationToken);

            if (!tasksResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to fetch tasks for project {ProjectName} ({ListId}): {StatusCode}",
                    projectName,
                    listId,
                    tasksResponse.StatusCode);
                return;
            }

            var tasksJson = await tasksResponse.Content.ReadAsStringAsync(cancellationToken);
            var tasksData = JsonDocument.Parse(tasksJson);
            var tasks = tasksData.RootElement.GetProperty("tasks");

            // Filter for TODO/REVIEW/BACKLOG tasks
            var todoTasks = new List<JsonElement>();
            var reviewTasks = new List<JsonElement>();
            var backlogTasks = new List<JsonElement>();

            foreach (var task in tasks.EnumerateArray())
            {
                var status = task.GetProperty("status").GetProperty("status").GetString()?.ToLower();

                switch (status)
                {
                    case "todo":
                        todoTasks.Add(task);
                        break;
                    case "review":
                        reviewTasks.Add(task);
                        break;
                    case "backlog":
                        backlogTasks.Add(task);
                        break;
                }
            }

            if (todoTasks.Count > 0 || reviewTasks.Count > 0 || backlogTasks.Count > 0)
            {
                _logger.LogInformation(
                    "Project {ProjectName}: {TodoCount} TODO, {ReviewCount} REVIEW, {BacklogCount} BACKLOG",
                    projectName,
                    todoTasks.Count,
                    reviewTasks.Count,
                    backlogTasks.Count);

                // Spawn agents for TODO tasks (using /implement-todo skill)
                foreach (var task in todoTasks)
                {
                    await SpawnAgentForTask(task, projectName, "todo", cancellationToken);
                }

                // Spawn agents for REVIEW tasks (using /clickup-reviewer skill)
                foreach (var task in reviewTasks)
                {
                    await SpawnAgentForTask(task, projectName, "review", cancellationToken);
                }

                // Spawn agents for BACKLOG tasks (using /clickup-refinement skill)
                foreach (var task in backlogTasks)
                {
                    await SpawnAgentForTask(task, projectName, "backlog", cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring tasks for project {ProjectName}", projectName);
        }
    }

    private async Task SpawnAgentForTask(
        JsonElement task,
        string projectName,
        string status,
        CancellationToken cancellationToken)
    {
        var taskId = task.GetProperty("id").GetString();
        var taskName = task.GetProperty("name").GetString();
        var taskUrl = task.GetProperty("url").GetString();

        _logger.LogInformation(
            "Spawning agent for {Status} task: [{TaskId}] {TaskName}",
            status.ToUpper(),
            taskId,
            taskName);

        // Determine which skill to use based on status
        var skill = status switch
        {
            "todo" => "/implement-todo",
            "review" => "/clickup-reviewer",
            "backlog" => "/clickup-refinement",
            _ => throw new ArgumentException($"Unknown status: {status}")
        };

        // Build Claude CLI command
        var prompt = $"{skill} {taskId}";

        // TODO: Actually spawn the Claude Code agent here
        // This would use the IClaudeInstanceManager service to spawn a new agent
        // with the configured model (_claudeModel) and the prompt

        _logger.LogDebug(
            "Agent spawn command: claude --model {Model} --print \"{Prompt}\"",
            _claudeModel,
            prompt);

        // Placeholder for actual agent spawning
        await Task.CompletedTask;
    }
}
