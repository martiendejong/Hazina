using System;
using System.Collections.Generic;
using System.IO;
using Hazina.TaskRunner.PowerShell;
using Xunit;

namespace Hazina.TaskRunner.Tests;

public class PowerShellExecutorTests : IDisposable
{
    private readonly PowerShellExecutor _executor;
    private readonly string _testScriptsDir;

    public PowerShellExecutorTests()
    {
        _executor = new PowerShellExecutor();
        _testScriptsDir = Path.Combine(Path.GetTempPath(), "HazinaTaskRunnerTests");
        Directory.CreateDirectory(_testScriptsDir);
    }

    [Fact]
    public void ExecuteScript_SimpleScript_Success()
    {
        // Arrange
        var scriptPath = Path.Combine(_testScriptsDir, "simple.ps1");
        File.WriteAllText(scriptPath, "Write-Output 'Hello from PowerShell'");

        // Act
        var result = _executor.ExecuteScript(scriptPath);

        // Assert
        Assert.True(result.Success, $"Expected success but got errors: {string.Join(", ", result.Errors)}");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Hello from PowerShell", result.Output);
        Assert.Empty(result.Errors);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public void ExecuteScript_ScriptWithError_FailsGracefully()
    {
        // Arrange
        var scriptPath = Path.Combine(_testScriptsDir, "error.ps1");
        File.WriteAllText(scriptPath, "throw 'Intentional error'");

        // Act
        var result = _executor.ExecuteScript(scriptPath);

        // Assert
        Assert.False(result.Success);
        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("Intentional error", string.Join(" ", result.Errors));
    }

    [Fact]
    public void ExecuteScript_NonExistentFile_ReturnsError()
    {
        // Arrange
        var scriptPath = Path.Combine(_testScriptsDir, "nonexistent.ps1");

        // Act
        var result = _executor.ExecuteScript(scriptPath);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Errors[0]);
    }

    [Fact]
    public void ExecuteScript_WithParameters_PassesCorrectly()
    {
        // Arrange
        var scriptPath = Path.Combine(_testScriptsDir, "params.ps1");
        File.WriteAllText(scriptPath, @"
param($Name, $Count)
Write-Output ""Hello $Name, count is $Count""
");

        var options = new ExecutionOptions
        {
            Parameters = new Dictionary<string, object>
            {
                ["Name"] = "Test",
                ["Count"] = 42
            }
        };

        // Act
        var result = _executor.ExecuteScript(scriptPath, options);

        // Assert
        Assert.True(result.Success, $"Expected success but got errors: {string.Join(", ", result.Errors)}");
        Assert.Contains("Hello Test", result.Output);
        Assert.Contains("count is 42", result.Output);
    }

    [Fact]
    public void ExecuteScript_LongRunning_TimesOut()
    {
        // Arrange
        var scriptPath = Path.Combine(_testScriptsDir, "slow.ps1");
        File.WriteAllText(scriptPath, "Start-Sleep -Seconds 10");

        var options = new ExecutionOptions
        {
            Timeout = TimeSpan.FromSeconds(1)
        };

        // Act
        var result = _executor.ExecuteScript(scriptPath, options);

        // Assert
        Assert.False(result.Success);
        Assert.True(result.TimedOut);
        Assert.Contains("timed out", string.Join(" ", result.Errors));
    }

    [Fact]
    public void ExecuteScript_VerboseOutput_CapturesOutput()
    {
        // Arrange
        var scriptPath = Path.Combine(_testScriptsDir, "verbose.ps1");
        File.WriteAllText(scriptPath, @"
Write-Verbose 'Verbose message' -Verbose
Write-Output 'Normal output'
");

        // Act
        var result = _executor.ExecuteScript(scriptPath);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Verbose message", result.Output);
        Assert.Contains("Normal output", result.Output);
    }

    [Fact]
    public void ExecuteCommand_SimpleCommand_Success()
    {
        // Arrange
        var command = "Write-Output 'Command executed'";

        // Act
        var result = _executor.ExecuteCommand(command);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Command executed", result.Output);
    }

    [Fact]
    public void ExecuteCommand_ComplexCommand_Success()
    {
        // Arrange
        var command = @"
$sum = 0
1..5 | ForEach-Object { $sum += $_ }
Write-Output ""Sum is $sum""
";

        // Act
        var result = _executor.ExecuteCommand(command);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Sum is 15", result.Output);
    }

    [Fact]
    public void ExecuteScript_MeasuresDuration_Accurately()
    {
        // Arrange
        var scriptPath = Path.Combine(_testScriptsDir, "timed.ps1");
        File.WriteAllText(scriptPath, "Start-Sleep -Milliseconds 500");

        // Act
        var result = _executor.ExecuteScript(scriptPath);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Duration.TotalMilliseconds >= 400);  // Allow some variance
        Assert.True(result.Duration.TotalMilliseconds < 1500); // Increased upper bound
    }

    [Fact]
    public void ExecuteScript_SilentMode_NoVisibleWindow()
    {
        // Arrange
        var scriptPath = Path.Combine(_testScriptsDir, "silent.ps1");
        File.WriteAllText(scriptPath, "Write-Output 'Silent execution'");

        var options = new ExecutionOptions
        {
            Silent = true
        };

        // Act
        var result = _executor.ExecuteScript(scriptPath, options);

        // Assert
        Assert.True(result.Success);
        // Note: Visual verification would require manual testing
        // This test verifies the option is accepted without errors
    }

    [Fact]
    public void ExecuteScript_SetsStartAndEndTime_Correctly()
    {
        // Arrange
        var scriptPath = Path.Combine(_testScriptsDir, "timestamps.ps1");
        File.WriteAllText(scriptPath, "Write-Output 'test'");

        var beforeExecution = DateTime.UtcNow;

        // Act
        var result = _executor.ExecuteScript(scriptPath);

        var afterExecution = DateTime.UtcNow;

        // Assert
        Assert.True(result.StartTime >= beforeExecution);
        Assert.True(result.EndTime <= afterExecution);
        Assert.True(result.EndTime > result.StartTime);
    }

    public void Dispose()
    {
        _executor.Dispose();

        if (Directory.Exists(_testScriptsDir))
        {
            try
            {
                // Wait briefly for PowerShell processes to fully exit
                System.Threading.Thread.Sleep(100);

                // Retry deletion a few times if locked
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
                        System.Threading.Thread.Sleep(200 * attempts);
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors - temp directory will be cleaned up by OS
            }
        }
    }
}
