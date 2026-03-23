using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AgenticOrchestration.Services.Monitoring;

/// <summary>
/// Monitors ClickUp for new TODO tasks that need autonomous execution
/// </summary>
public class ClickUpEventMonitor
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly List<string> _listIds;
    private DateTime _lastCheck = DateTime.UtcNow;

    public ClickUpEventMonitor(string apiKey, List<string> listIds)
    {
        _apiKey = apiKey;
        _listIds = listIds;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", apiKey);
    }

    /// <summary>
    /// Poll ClickUp for new TODO tasks
    /// Returns tasks that:
    /// - Status = "todo"
    /// - Updated since last check
    /// - Have clear descriptions (not "needs input")
    /// </summary>
    public async Task<List<ClickUpTask>> PollForNewTasks(CancellationToken cancellationToken)
    {
        var newTasks = new List<ClickUpTask>();

        foreach (var listId in _listIds)
        {
            try
            {
                // ClickUp API: Get tasks from list
                var url = $"https://api.clickup.com/api/v2/list/{listId}/task?statuses[]=todo&order_by=updated&reverse=true";
                var response = await _httpClient.GetFromJsonAsync<ClickUpTasksResponse>(url, cancellationToken);

                if (response?.Tasks != null)
                {
                    // Filter for tasks updated since last check
                    var recentTasks = response.Tasks
                        .Where(t => t.DateUpdated > _lastCheck)
                        .Where(t => !string.IsNullOrWhiteSpace(t.Description))
                        .ToList();

                    newTasks.AddRange(recentTasks);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error polling ClickUp list {listId}: {ex.Message}");
            }
        }

        _lastCheck = DateTime.UtcNow;
        return newTasks;
    }
}

public class ClickUpTasksResponse
{
    public List<ClickUpTask> Tasks { get; set; } = new();
}

public class ClickUpTask
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ClickUpStatus Status { get; set; } = new();
    public DateTime DateCreated { get; set; }
    public DateTime DateUpdated { get; set; }
    public int Priority { get; set; } // 1=urgent, 2=high, 3=normal, 4=low
    public List<string> Tags { get; set; } = new();
}

public class ClickUpStatus
{
    public string Status { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
}
