using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hazina.AgenticOrchestration.Services.Execution;
using Hazina.AgenticOrchestration.Services.Monitoring;

namespace Hazina.AgenticOrchestration.Services;

/// <summary>
/// Autonomous background worker service
/// Continuously monitors ClickUp/GitHub, queues work, and executes autonomously
/// This is the 24/7 orchestration loop that keeps Jengo running
/// </summary>
public class AutonomousWorkerService
{
    private readonly ClickUpEventMonitor _clickUpMonitor;
    private readonly GitHubEventMonitor _githubMonitor;
    private readonly WorkPriorityQueue _workQueue;
    private readonly AutonomousExecutor _executor;

    private readonly TimeSpan _pollInterval = TimeSpan.FromMinutes(5); // Check for new work every 5 minutes
    private readonly int _maxConcurrentExecutions = 1; // Execute one task at a time (for now)
    private int _runningExecutions = 0;

    public AutonomousWorkerService(
        ClickUpEventMonitor clickUpMonitor,
        GitHubEventMonitor githubMonitor,
        WorkPriorityQueue workQueue,
        AutonomousExecutor executor)
    {
        _clickUpMonitor = clickUpMonitor;
        _githubMonitor = githubMonitor;
        _workQueue = workQueue;
        _executor = executor;
    }

    /// <summary>
    /// Start the autonomous worker loop
    /// This runs indefinitely until cancellation is requested
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[AutonomousWorker] Starting 24/7 autonomous loop...");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Phase 1: Poll for new work
                await PollForWork(cancellationToken);

                // Phase 2: Execute next task if capacity available
                await ExecuteNextTask(cancellationToken);

                // Phase 3: Sleep until next poll
                await Task.Delay(_pollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[AutonomousWorker] Cancellation requested, stopping...");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutonomousWorker] Loop error: {ex.Message}");
                // Continue running despite errors
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
        }

        Console.WriteLine("[AutonomousWorker] Stopped.");
    }

    /// <summary>
    /// Poll ClickUp and GitHub for new work
    /// </summary>
    private async Task PollForWork(CancellationToken cancellationToken)
    {
        Console.WriteLine($"[AutonomousWorker] Polling for new work... (Queue: {_workQueue.Count})");

        // Poll ClickUp
        try
        {
            var clickUpTasks = await _clickUpMonitor.PollForNewTasks(cancellationToken);
            if (clickUpTasks.Count > 0)
            {
                Console.WriteLine($"[AutonomousWorker] Found {clickUpTasks.Count} new ClickUp tasks");
                _workQueue.EnqueueTasks(clickUpTasks);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AutonomousWorker] ClickUp polling error: {ex.Message}");
        }

        // Poll GitHub
        try
        {
            var githubEvents = await _githubMonitor.PollForNewEvents(cancellationToken);
            if (githubEvents.Count > 0)
            {
                Console.WriteLine($"[AutonomousWorker] Found {githubEvents.Count} new GitHub events");
                _workQueue.EnqueueEvents(githubEvents);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AutonomousWorker] GitHub polling error: {ex.Message}");
        }

        Console.WriteLine($"[AutonomousWorker] Polling complete. Queue size: {_workQueue.Count}");
    }

    /// <summary>
    /// Execute next task from queue if capacity available
    /// </summary>
    private async Task ExecuteNextTask(CancellationToken cancellationToken)
    {
        // Check if we have capacity to execute
        if (_runningExecutions >= _maxConcurrentExecutions)
        {
            Console.WriteLine($"[AutonomousWorker] Max concurrency reached ({_maxConcurrentExecutions}), waiting...");
            return;
        }

        // Get next work item
        var workItem = _workQueue.Dequeue();
        if (workItem == null)
        {
            return; // No work available
        }

        // Execute asynchronously (fire and forget)
        _ = Task.Run(async () =>
        {
            Interlocked.Increment(ref _runningExecutions);
            try
            {
                Console.WriteLine($"[AutonomousWorker] Executing: {workItem.Title} (Priority: {workItem.Priority})");
                var result = await _executor.ExecuteAsync(workItem, cancellationToken);

                if (result.Success)
                {
                    Console.WriteLine($"[AutonomousWorker] ✅ Success: {workItem.Title} ({result.Duration.TotalMinutes:F1}min)");
                }
                else
                {
                    Console.WriteLine($"[AutonomousWorker] ❌ Failed: {workItem.Title}");
                    Console.WriteLine($"[AutonomousWorker] Output: {result.Output}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutonomousWorker] Execution error: {ex.Message}");
            }
            finally
            {
                Interlocked.Decrement(ref _runningExecutions);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Get current status
    /// </summary>
    public WorkerStatus GetStatus()
    {
        return new WorkerStatus
        {
            QueueSize = _workQueue.Count,
            RunningExecutions = _runningExecutions,
            NextTask = _workQueue.Peek()
        };
    }
}

public class WorkerStatus
{
    public int QueueSize { get; set; }
    public int RunningExecutions { get; set; }
    public WorkItem? NextTask { get; set; }
}
