using System;
using System.IO;
using System.Threading;
using Hazina.TaskRunner.Scheduling;
using Xunit;
using TaskScheduler = Hazina.TaskRunner.Scheduling.TaskScheduler;

namespace Hazina.TaskRunner.Tests.Scheduling;

public class TaskSchedulerTests : IDisposable
{
    private readonly string _testConfigPath;
    private readonly string _testScriptsDir;
    private const string TestTaskSetId = "test-task-set";

    public TaskSchedulerTests()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"scheduler-test-{Guid.NewGuid()}");
        _testScriptsDir = Path.Combine(Path.GetTempPath(), "SchedulerTestScripts");
        Directory.CreateDirectory(_testScriptsDir);
        Directory.CreateDirectory(_testConfigPath);
    }

    /// <summary>
    /// Helper to create and save a test TaskSet
    /// </summary>
    private void CreateTestTaskSet(TaskScheduler scheduler)
    {
        var taskSetConfig = new TaskSetConfiguration(_testConfigPath);
        var taskSet = new TaskSet
        {
            Id = TestTaskSetId,
            Name = "Test Task Set",
            Description = "Task set for unit tests",
            Enabled = true
        };
        taskSetConfig.SaveTaskSet(taskSet);
        // Reload to pick up the new task set
        var loadMethod = typeof(TaskScheduler).GetMethod("LoadTaskSets",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        loadMethod?.Invoke(scheduler, null);
    }

    [Fact]
    public void AddTask_NewTask_SavesSuccessfully()
    {
        // Arrange
        using var scheduler = new TaskScheduler(_testConfigPath);
        CreateTestTaskSet(scheduler);
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Test Task",
            ScriptPath = Path.Combine(_testScriptsDir, "test.ps1"),
            CronExpression = "0 0 * * *"  // Daily at midnight
        };

        // Act
        scheduler.AddTask(TestTaskSetId, task);
        var retrieved = scheduler.GetTask("test-task");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("test-task", retrieved.Id);
        Assert.Equal("Test Task", retrieved.Name);
        Assert.NotNull(retrieved.NextRun);
    }

    [Fact]
    public void UpdateTask_ExistingTask_UpdatesSuccessfully()
    {
        // Arrange
        using var scheduler = new TaskScheduler(_testConfigPath);
        CreateTestTaskSet(scheduler);
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Original Name",
            ScriptPath = Path.Combine(_testScriptsDir, "test.ps1"),
            CronExpression = "0 0 * * *"
        };
        scheduler.AddTask(TestTaskSetId, task);

        // Act
        task.Name = "Updated Name";
        task.CronExpression = "0 6 * * *";  // Changed to 6am
        scheduler.UpdateTask(TestTaskSetId, task);
        var retrieved = scheduler.GetTask("test-task");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Updated Name", retrieved.Name);
        Assert.Equal("0 6 * * *", retrieved.CronExpression);
    }

    [Fact]
    public void RemoveTask_ExistingTask_RemovesSuccessfully()
    {
        // Arrange
        using var scheduler = new TaskScheduler(_testConfigPath);
        CreateTestTaskSet(scheduler);
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Test Task",
            ScriptPath = Path.Combine(_testScriptsDir, "test.ps1"),
            CronExpression = "0 0 * * *"
        };
        scheduler.AddTask(TestTaskSetId, task);

        // Act
        var removed = scheduler.RemoveTask("test-task");
        var retrieved = scheduler.GetTask("test-task");

        // Assert
        Assert.True(removed);
        Assert.Null(retrieved);
    }

    [Fact]
    public void GetAllTasks_MultipleTasks_ReturnsAll()
    {
        // Arrange
        using var scheduler = new TaskScheduler(_testConfigPath);
        CreateTestTaskSet(scheduler);
        scheduler.AddTask(TestTaskSetId, new ScheduledTask { Id = "task1", Name = "Task 1", ScriptPath = "test.ps1", CronExpression = "0 0 * * *" });
        scheduler.AddTask(TestTaskSetId, new ScheduledTask { Id = "task2", Name = "Task 2", ScriptPath = "test.ps1", CronExpression = "0 6 * * *" });
        scheduler.AddTask(TestTaskSetId, new ScheduledTask { Id = "task3", Name = "Task 3", ScriptPath = "test.ps1", CronExpression = "0 12 * * *" });

        // Act
        var tasks = scheduler.GetAllTasks();

        // Assert
        Assert.Equal(3, tasks.Count);
        Assert.Contains(tasks, t => t.Id == "task1");
        Assert.Contains(tasks, t => t.Id == "task2");
        Assert.Contains(tasks, t => t.Id == "task3");
    }

    [Fact]
    public void EnableTask_DisabledTask_EnablesSuccessfully()
    {
        // Arrange
        using var scheduler = new TaskScheduler(_testConfigPath);
        CreateTestTaskSet(scheduler);
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Test Task",
            ScriptPath = "test.ps1",
            CronExpression = "0 0 * * *",
            Enabled = false
        };
        scheduler.AddTask(TestTaskSetId, task);

        // Act
        scheduler.EnableTask("test-task");
        var retrieved = scheduler.GetTask("test-task");

        // Assert
        Assert.NotNull(retrieved);
        Assert.True(retrieved.Enabled);
        Assert.NotNull(retrieved.NextRun);
    }

    [Fact]
    public void DisableTask_EnabledTask_DisablesSuccessfully()
    {
        // Arrange
        using var scheduler = new TaskScheduler(_testConfigPath);
        CreateTestTaskSet(scheduler);
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Test Task",
            ScriptPath = "test.ps1",
            CronExpression = "0 0 * * *",
            Enabled = true
        };
        scheduler.AddTask(TestTaskSetId, task);

        // Act
        scheduler.DisableTask("test-task");
        var retrieved = scheduler.GetTask("test-task");

        // Assert
        Assert.NotNull(retrieved);
        Assert.False(retrieved.Enabled);
    }

    [Fact]
    public void RunTaskNow_ValidTask_ExecutesImmediately()
    {
        // Arrange
        var scriptPath = Path.Combine(_testScriptsDir, "test-output.ps1");
        var outputPath = Path.Combine(_testScriptsDir, "output.txt");
        File.WriteAllText(scriptPath, $"'Executed' | Out-File '{outputPath}'");

        using var scheduler = new TaskScheduler(_testConfigPath);
        CreateTestTaskSet(scheduler);
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Test Task",
            ScriptPath = scriptPath,
            CronExpression = "0 0 * * *"
        };
        scheduler.AddTask(TestTaskSetId, task);

        // Act
        scheduler.RunTaskNow("test-task");

        // Wait briefly for execution
        Thread.Sleep(1000);

        // Assert
        Assert.True(File.Exists(outputPath), "Script should have created output file");
        var output = File.ReadAllText(outputPath);
        Assert.Contains("Executed", output);

        var retrieved = scheduler.GetTask("test-task");
        Assert.NotNull(retrieved?.LastRun);
    }

    [Fact]
    public void CronExpression_DailyAtMidnight_CalculatesNextRunCorrectly()
    {
        // Arrange
        using var scheduler = new TaskScheduler(_testConfigPath);
        CreateTestTaskSet(scheduler);
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Daily Task",
            ScriptPath = "test.ps1",
            CronExpression = "0 0 * * *"  // Daily at midnight UTC
        };

        // Act
        scheduler.AddTask(TestTaskSetId, task);
        var retrieved = scheduler.GetTask("test-task");

        // Assert
        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.NextRun);

        // Next run should be at midnight UTC (hour 0, minute 0)
        Assert.Equal(0, retrieved.NextRun.Value.Hour);
        Assert.Equal(0, retrieved.NextRun.Value.Minute);

        // Should be in the future
        Assert.True(retrieved.NextRun > DateTime.UtcNow);
    }

    [Fact]
    public void CronExpression_EveryMinute_CalculatesNextRunCorrectly()
    {
        // Arrange
        using var scheduler = new TaskScheduler(_testConfigPath);
        CreateTestTaskSet(scheduler);
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Every Minute Task",
            ScriptPath = "test.ps1",
            CronExpression = "* * * * *"  // Every minute
        };

        // Act
        scheduler.AddTask(TestTaskSetId, task);
        var retrieved = scheduler.GetTask("test-task");

        // Assert
        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.NextRun);

        // Next run should be within next 2 minutes
        var timeDiff = retrieved.NextRun.Value - DateTime.UtcNow;
        Assert.True(timeDiff.TotalMinutes <= 2, $"Next run should be within 2 minutes, but is {timeDiff.TotalMinutes:F2} minutes away");
    }

    public void Dispose()
    {
        // Cleanup test files
        try
        {
            if (Directory.Exists(_testConfigPath))
            {
                Thread.Sleep(100);  // Brief wait for file handles to release

                int attempts = 0;
                while (attempts < 3 && Directory.Exists(_testConfigPath))
                {
                    try
                    {
                        Directory.Delete(_testConfigPath, recursive: true);
                        break;
                    }
                    catch (IOException)
                    {
                        attempts++;
                        Thread.Sleep(200 * attempts);
                    }
                }
            }

            if (Directory.Exists(_testScriptsDir))
            {
                Thread.Sleep(100);  // Brief wait for file handles to release

                int attempts = 0;
                while (attempts < 3 && Directory.Exists(_testScriptsDir))
                {
                    try
                    {
                        Directory.Delete(_testScriptsDir, recursive: true);
                        break;
                    }
                    catch (IOException)
                    {
                        attempts++;
                        Thread.Sleep(200 * attempts);
                    }
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
