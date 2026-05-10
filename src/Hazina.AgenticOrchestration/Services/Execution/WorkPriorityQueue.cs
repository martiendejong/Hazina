using System;
using System.Collections.Generic;
using System.Linq;
using Hazina.AgenticOrchestration.Services.Monitoring;

namespace Hazina.AgenticOrchestration.Services.Execution;

/// <summary>
/// Priority queue for autonomous work items
/// Combines ClickUp tasks and GitHub events into a single prioritized queue
/// </summary>
public class WorkPriorityQueue
{
    private readonly List<WorkItem> _queue = new();
    private readonly object _lock = new();

    /// <summary>
    /// Add ClickUp tasks to the queue
    /// </summary>
    public void EnqueueTasks(List<ClickUpTask> tasks)
    {
        lock (_lock)
        {
            foreach (var task in tasks)
            {
                // Skip if already in queue
                if (_queue.Any(w => w.Source == WorkSource.ClickUp && w.Id == task.Id))
                    continue;

                _queue.Add(new WorkItem
                {
                    Source = WorkSource.ClickUp,
                    Id = task.Id,
                    Title = task.Name,
                    Description = task.Description,
                    Url = $"https://app.clickup.com/t/{task.Id}",
                    Priority = task.Priority,
                    EnqueuedAt = DateTime.UtcNow
                });
            }
        }
    }

    /// <summary>
    /// Add GitHub events to the queue
    /// </summary>
    public void EnqueueEvents(List<GitHubEvent> events)
    {
        lock (_lock)
        {
            foreach (var evt in events)
            {
                // Skip if already in queue
                if (_queue.Any(w => w.Source == WorkSource.GitHub && w.Id == evt.Id))
                    continue;

                _queue.Add(new WorkItem
                {
                    Source = WorkSource.GitHub,
                    Id = evt.Id,
                    Title = evt.Title,
                    Description = $"GitHub {evt.Type}: {evt.Title}",
                    Url = evt.Url,
                    Priority = evt.Priority,
                    EnqueuedAt = DateTime.UtcNow
                });
            }
        }
    }

    /// <summary>
    /// Get next work item by priority
    /// Priority order:
    /// 1. Urgent (priority 1)
    /// 2. High (priority 2)
    /// 3. Normal (priority 3)
    /// 4. Low (priority 4)
    /// Within same priority: FIFO (oldest first)
    /// </summary>
    public WorkItem? Dequeue()
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
                return null;

            var next = _queue
                .OrderBy(w => w.Priority) // Lower number = higher priority
                .ThenBy(w => w.EnqueuedAt) // Oldest first
                .First();

            _queue.Remove(next);
            return next;
        }
    }

    /// <summary>
    /// Get current queue size
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }
    }

    /// <summary>
    /// Peek at next item without removing it
    /// </summary>
    public WorkItem? Peek()
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
                return null;

            return _queue
                .OrderBy(w => w.Priority)
                .ThenBy(w => w.EnqueuedAt)
                .First();
        }
    }

    /// <summary>
    /// Clear all items from queue
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _queue.Clear();
        }
    }
}

public class WorkItem
{
    public WorkSource Source { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime EnqueuedAt { get; set; }
}

public enum WorkSource
{
    ClickUp,
    GitHub
}
