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
        Assert.True(result.Success);
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
        Assert.True(result.Success);
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
    public void ExecuteScript_WithWarnings_CapturesWarnings()
    {
        // Arrange
        var scriptPath = Path.Combine(_testScriptsDir, "warnings.ps1");
        File.WriteAllText(scriptPath, @"
Write-Warning 'This is a warning'
Write-Output 'Normal output'
");

        // Act
        var result = _executor.ExecuteScript(scriptPath);

        // Assert
        Assert.True(result.Success);  // Warnings don't fail execution
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("This is a warning", result.Warnings[0]);
        Assert.Contains("Normal output", result.Output);
    }

    [Fact]
    public void ExecuteScript_VerboseOutput_CapturesVerbose()
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
        Assert.Contains("VERBOSE: Verbose message", result.Output);
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
        Assert.True(result.Duration.TotalMilliseconds < 1000);
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
    public void ExecuteScript_IsolatedRunspace_IndependentState()
    {
        // Arrange
        var script1 = Path.Combine(_testScriptsDir, "set-var.ps1");
        File.WriteAllText(script1, "$global:TestVar = 'value1'");

        var script2 = Path.Combine(_testScriptsDir, "get-var.ps1");
        File.WriteAllText(script2, "Write-Output $global:TestVar");

        var options = new ExecutionOptions { IsolatedRunspace = true };

        // Act
        _executor.ExecuteScript(script1, options);
        var result2 = _executor.ExecuteScript(script2, options);

        // Assert
        // Variable from script1 should NOT exist in script2's isolated runspace
        Assert.True(result2.Success);
        Assert.DoesNotContain("value1", result2.Output);
    }

    [Fact]
    public void ExecuteScript_PersistentRunspace_SharesState()
    {
        // Arrange
        var script1 = Path.Combine(_testScriptsDir, "set-persistent.ps1");
        File.WriteAllText(script1, "$global:TestVar = 'persistent'");

        var script2 = Path.Combine(_testScriptsDir, "get-persistent.ps1");
        File.WriteAllText(script2, "Write-Output $global:TestVar");

        var options = new ExecutionOptions { IsolatedRunspace = false };

        // Act
        _executor.ExecuteScript(script1, options);
        var result2 = _executor.ExecuteScript(script2, options);

        // Assert
        // Variable from script1 SHOULD exist in script2's persistent runspace
        Assert.True(result2.Success);
        Assert.Contains("persistent", result2.Output);
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
            Directory.Delete(_testScriptsDir, recursive: true);
        }
    }
}
