using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hazina.App.HazinaCoder.Core.ClickUp;

/// <summary>
/// 🚀 IMPROVEMENT #3 (ROI 3.0): ClickUp API Integration
/// Automatically hydrates task requirements - can't fix bugs without reading descriptions!
/// Prevents "placeholder implementation" disasters
/// </summary>
public class ClickUpClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.clickup.com/api/v2";

    public ClickUpClient(string apiKey)
    {
        _apiKey = apiKey;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(_apiKey);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Get full task details with description, comments, attachments, custom fields
    /// </summary>
    public async Task<ClickUpTask?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync($"{BaseUrl}/task/{taskId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var task = await response.Content.ReadFromJsonAsync<ClickUpTask>(cancellationToken: cancellationToken);
            return task;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClickUp] Failed to fetch task {taskId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get all comments on a task (includes error logs, screenshots, reproduction steps)
    /// </summary>
    public async Task<List<ClickUpComment>> GetTaskCommentsAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync($"{BaseUrl}/task/{taskId}/comment", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ClickUpCommentsResponse>(cancellationToken: cancellationToken);
            return result?.Comments ?? new List<ClickUpComment>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClickUp] Failed to fetch comments for task {taskId}: {ex.Message}");
            return new List<ClickUpComment>();
        }
    }

    /// <summary>
    /// Post a comment to a task (for asking clarification questions)
    /// </summary>
    public async Task<bool> PostCommentAsync(string taskId, string commentText, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { comment_text = commentText };
            var response = await _http.PostAsJsonAsync($"{BaseUrl}/task/{taskId}/comment", payload, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClickUp] Failed to post comment to task {taskId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Update task status (e.g., "in progress", "ready for review")
    /// </summary>
    public async Task<bool> UpdateTaskStatusAsync(string taskId, string status, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { status };
            var response = await _http.PutAsJsonAsync($"{BaseUrl}/task/{taskId}", payload, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClickUp] Failed to update status for task {taskId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Add PR link to task description or custom field
    /// </summary>
    public async Task<bool> LinkPullRequestAsync(string taskId, string prUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            // Post as comment so it's immediately visible
            var commentText = $"✅ **Pull Request Created:** {prUrl}";
            return await PostCommentAsync(taskId, commentText, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClickUp] Failed to link PR to task {taskId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get unassigned tasks in "todo" status for a list
    /// </summary>
    public async Task<List<ClickUpTask>> GetUnassignedTodoTasksAsync(string listId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync(
                $"{BaseUrl}/list/{listId}/task?statuses[]=todo&assignees[]=unassigned&subtasks=true",
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ClickUpTasksResponse>(cancellationToken: cancellationToken);
            return result?.Tasks ?? new List<ClickUpTask>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClickUp] Failed to fetch unassigned tasks for list {listId}: {ex.Message}");
            return new List<ClickUpTask>();
        }
    }
}

/// <summary>
/// Hydrated task requirements - everything needed to implement the task
/// </summary>
public class TaskRequirements
{
    public string TaskId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public List<ClickUpComment> Comments { get; set; } = new();
    public List<ClickUpAttachment> Attachments { get; set; } = new();
    public Dictionary<string, string> CustomFields { get; set; } = new();
    public DateTime? DueDate { get; set; }
    public int? Priority { get; set; }

    /// <summary>
    /// Generate a comprehensive requirements summary for LLM context
    /// </summary>
    public string ToContextString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Task Requirements: {Title}");
        sb.AppendLine($"**ID:** {TaskId}");
        sb.AppendLine($"**Status:** {Status}");

        if (Priority.HasValue)
            sb.AppendLine($"**Priority:** {Priority.Value}/4");

        if (DueDate.HasValue)
            sb.AppendLine($"**Due Date:** {DueDate.Value:yyyy-MM-dd}");

        if (Tags.Any())
            sb.AppendLine($"**Tags:** {string.Join(", ", Tags)}");

        sb.AppendLine();
        sb.AppendLine("## Description");
        sb.AppendLine(Description);

        if (Comments.Any())
        {
            sb.AppendLine();
            sb.AppendLine("## Comments & Discussion");
            foreach (var comment in Comments.OrderBy(c => c.Date))
            {
                sb.AppendLine($"### {comment.User?.Username ?? "Unknown"} - {comment.Date:yyyy-MM-dd HH:mm}");
                sb.AppendLine(comment.CommentText);
                sb.AppendLine();
            }
        }

        if (Attachments.Any())
        {
            sb.AppendLine();
            sb.AppendLine("## Attachments");
            foreach (var attachment in Attachments)
            {
                sb.AppendLine($"- [{attachment.Name}]({attachment.Url}) ({attachment.Size} bytes)");
            }
        }

        if (CustomFields.Any())
        {
            sb.AppendLine();
            sb.AppendLine("## Custom Fields");
            foreach (var field in CustomFields)
            {
                sb.AppendLine($"- **{field.Key}:** {field.Value}");
            }
        }

        return sb.ToString();
    }
}

#region ClickUp API Response Models

public class ClickUpTask
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("status")]
    public ClickUpStatus? Status { get; set; }

    [JsonPropertyName("tags")]
    public List<ClickUpTag> Tags { get; set; } = new();

    [JsonPropertyName("assignees")]
    public List<ClickUpUser> Assignees { get; set; } = new();

    [JsonPropertyName("due_date")]
    public string? DueDate { get; set; }

    [JsonPropertyName("priority")]
    public ClickUpPriority? Priority { get; set; }

    [JsonPropertyName("custom_fields")]
    public List<ClickUpCustomField> CustomFields { get; set; } = new();

    [JsonPropertyName("attachments")]
    public List<ClickUpAttachment> Attachments { get; set; } = new();

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    /// <summary>
    /// Convert to hydrated requirements object
    /// </summary>
    public TaskRequirements ToRequirements(List<ClickUpComment>? comments = null)
    {
        return new TaskRequirements
        {
            TaskId = Id,
            Title = Name,
            Description = Description ?? "",
            Status = Status?.Status ?? "unknown",
            Tags = Tags.Select(t => t.Name).ToList(),
            Comments = comments ?? new List<ClickUpComment>(),
            Attachments = Attachments,
            CustomFields = CustomFields.ToDictionary(cf => cf.Name, cf => cf.Value?.ToString() ?? ""),
            DueDate = !string.IsNullOrEmpty(DueDate) ? DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(DueDate)).DateTime : null,
            Priority = Priority?.Priority
        };
    }
}

public class ClickUpStatus
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "";
}

public class ClickUpTag
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class ClickUpUser
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";
}

public class ClickUpPriority
{
    [JsonPropertyName("priority")]
    public int Priority { get; set; }
}

public class ClickUpCustomField
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value")]
    public JsonElement? Value { get; set; }
}

public class ClickUpAttachment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Name { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("mimetype")]
    public string MimeType { get; set; } = "";
}

public class ClickUpComment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("comment_text")]
    public string CommentText { get; set; } = "";

    [JsonPropertyName("user")]
    public ClickUpUser? User { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}

public class ClickUpTasksResponse
{
    [JsonPropertyName("tasks")]
    public List<ClickUpTask> Tasks { get; set; } = new();
}

public class ClickUpCommentsResponse
{
    [JsonPropertyName("comments")]
    public List<ClickUpComment> Comments { get; set; } = new();
}

#endregion
