using System;
using System.IO;
using System.Threading;
using Hazina.TaskRunner.Scheduling;
using Xunit;

namespace Hazina.TaskRunner.Tests.Scheduling;

public class TaskSchedulerTests : IDisposable
{
    private readonly string _testConfigPath;
    private readonly string _testScriptsDir;

    public TaskSchedulerTests()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"scheduler-test-{Guid.NewGuid()}.json");
        _testScriptsDir = Path.Combine(Path.GetTempPath(), "SchedulerTestScripts");
        Directory.CreateDirectory(_testScriptsDir);
    }

    [Fact]
    public void AddTask_NewTask_SavesSuccessfully()
    {
        // Arrange
        using var scheduler = new TaskScheduler(_testConfigPath);
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Test Task",
            ScriptPath = Path.Combine(_testScriptsDir, "test.ps1"),
            CronExpression = "0 0 * * *"  // Daily at midnight
        };

        // Act
        scheduler.AddTask(task);
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
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Original Name",
            ScriptPath = Path.Combine(_testScriptsDir, "test.ps1"),
            CronExpression = "0 0 * * *"
        };
        scheduler.AddTask(task);

        // Act
        task.Name = "Updated Name";
        task.CronExpression = "0 6 * * *";  // Changed to 6am
        scheduler.UpdateTask(task);
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
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Test Task",
            ScriptPath = Path.Combine(_testScriptsDir, "test.ps1"),
            CronExpression = "0 0 * * *"
        };
        scheduler.AddTask(task);

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
        scheduler.AddTask(new ScheduledTask { Id = "task1", Name = "Task 1", ScriptPath = "test.ps1", CronExpression = "0 0 * * *" });
        scheduler.AddTask(new ScheduledTask { Id = "task2", Name = "Task 2", ScriptPath = "test.ps1", CronExpression = "0 6 * * *" });
        scheduler.AddTask(new ScheduledTask { Id = "task3", Name = "Task 3", ScriptPath = "test.ps1", CronExpression = "0 12 * * *" });

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
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Test Task",
            ScriptPath = "test.ps1",
            CronExpression = "0 0 * * *",
            Enabled = false
        };
        scheduler.AddTask(task);

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
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Test Task",
            ScriptPath = "test.ps1",
            CronExpression = "0 0 * * *",
            Enabled = true
        };
        scheduler.AddTask(task);

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
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Test Task",
            ScriptPath = scriptPath,
            CronExpression = "0 0 * * *"
        };
        scheduler.AddTask(task);

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
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Daily Task",
            ScriptPath = "test.ps1",
            CronExpression = "0 0 * * *"  // Daily at midnight UTC
        };

        // Act
        scheduler.AddTask(task);
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
        var task = new ScheduledTask
        {
            Id = "test-task",
            Name = "Every Minute Task",
            ScriptPath = "test.ps1",
            CronExpression = "* * * * *"  // Every minute
        };

        // Act
        scheduler.AddTask(task);
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
            if (File.Exists(_testConfigPath))
            {
                File.Delete(_testConfigPath);
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
