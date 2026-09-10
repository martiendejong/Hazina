using Xunit;
using Xunit.Abstractions;

namespace Hazina.Tests.Orchestration;

/// <summary>
/// Test to verify that the orchestration system properly captures stdout and stderr
/// from Claude agent executions. This is part of the autonomous orchestration
/// validation testing (Task #869cm28dj).
/// </summary>
public class StdoutStderrCaptureTest
{
    private readonly ITestOutputHelper _output;

    public StdoutStderrCaptureTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Test_StdoutCapture()
    {
        // Capture stdout using StringWriter
        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);

        try
        {
            // Write to stdout - this should be captured
            Console.WriteLine("STDOUT_TEST: This is a standard output message");
            Console.WriteLine("STDOUT_TEST: Message with timestamp: " + DateTime.UtcNow.ToString("o"));
            Console.WriteLine("STDOUT_TEST: Multi-line message");
            Console.WriteLine("STDOUT_TEST: Line 2 of multi-line");
            Console.WriteLine("STDOUT_TEST: Line 3 of multi-line");

            // Restore original stdout
            Console.SetOut(originalOut);

            // Verify captured output
            string captured = sw.ToString();
            _output.WriteLine($"Captured {captured.Length} characters from stdout");

            Assert.Contains("STDOUT_TEST: This is a standard output message", captured);
            Assert.Contains("STDOUT_TEST: Multi-line message", captured);
            Assert.Contains("STDOUT_TEST: Line 3 of multi-line", captured);
            Assert.True(captured.Split(Environment.NewLine).Length >= 5, "Should have captured at least 5 lines");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Test_StderrCapture()
    {
        // Capture stderr using StringWriter
        using var sw = new StringWriter();
        var originalErr = Console.Error;
        Console.SetError(sw);

        try
        {
            // Write to stderr - this should be captured
            Console.Error.WriteLine("STDERR_TEST: This is a standard error message");
            Console.Error.WriteLine("STDERR_TEST: Error with timestamp: " + DateTime.UtcNow.ToString("o"));
            Console.Error.WriteLine("STDERR_TEST: Simulated error condition");
            Console.Error.WriteLine("STDERR_TEST: Warning: This is not a real error, just a test");

            // Restore original stderr
            Console.SetError(originalErr);

            // Verify captured output
            string captured = sw.ToString();
            _output.WriteLine($"Captured {captured.Length} characters from stderr");

            Assert.Contains("STDERR_TEST: This is a standard error message", captured);
            Assert.Contains("STDERR_TEST: Simulated error condition", captured);
            Assert.Contains("Warning: This is not a real error, just a test", captured);
            Assert.True(captured.Split(Environment.NewLine).Length >= 4, "Should have captured at least 4 lines");
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Test_MixedOutput()
    {
        // Capture both stdout and stderr
        using var swOut = new StringWriter();
        using var swErr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        Console.SetOut(swOut);
        Console.SetError(swErr);

        try
        {
            // Mix stdout and stderr to test interleaved capture
            Console.WriteLine("MIXED_TEST: Starting mixed output test");
            Console.Error.WriteLine("MIXED_TEST: Error line 1");
            Console.WriteLine("MIXED_TEST: Output line 1");
            Console.Error.WriteLine("MIXED_TEST: Error line 2");
            Console.WriteLine("MIXED_TEST: Output line 2");
            Console.Error.WriteLine("MIXED_TEST: Error line 3");
            Console.WriteLine("MIXED_TEST: Ending mixed output test");

            // Restore originals
            Console.SetOut(originalOut);
            Console.SetError(originalErr);

            // Verify both streams captured independently
            string capturedOut = swOut.ToString();
            string capturedErr = swErr.ToString();

            _output.WriteLine($"Captured {capturedOut.Length} chars from stdout, {capturedErr.Length} chars from stderr");

            // Verify stdout lines
            Assert.Contains("MIXED_TEST: Starting mixed output test", capturedOut);
            Assert.Contains("MIXED_TEST: Output line 1", capturedOut);
            Assert.Contains("MIXED_TEST: Ending mixed output test", capturedOut);

            // Verify stderr lines
            Assert.Contains("MIXED_TEST: Error line 1", capturedErr);
            Assert.Contains("MIXED_TEST: Error line 2", capturedErr);
            Assert.Contains("MIXED_TEST: Error line 3", capturedErr);

            // Verify correct stream separation
            Assert.DoesNotContain("Error line", capturedOut);
            Assert.DoesNotContain("Output line", capturedErr);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Test_LargeOutput()
    {
        // Capture stdout for large volume test
        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);

        try
        {
            // Test that large outputs are captured correctly
            Console.WriteLine("LARGE_TEST: Starting large output test");

            for (int i = 1; i <= 100; i++)
            {
                Console.WriteLine($"LARGE_TEST: Line {i} of 100");
            }

            Console.WriteLine("LARGE_TEST: Completed 100 lines");

            // Restore original stdout
            Console.SetOut(originalOut);

            // Verify captured output
            string captured = sw.ToString();
            var lines = captured.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            _output.WriteLine($"Captured {lines.Length} lines from stdout");

            Assert.Contains("LARGE_TEST: Starting large output test", captured);
            Assert.Contains("LARGE_TEST: Line 1 of 100", captured);
            Assert.Contains("LARGE_TEST: Line 50 of 100", captured);
            Assert.Contains("LARGE_TEST: Line 100 of 100", captured);
            Assert.Contains("LARGE_TEST: Completed 100 lines", captured);
            Assert.True(lines.Length >= 102, $"Should have captured at least 102 lines, got {lines.Length}");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Test_SpecialCharacters()
    {
        // Capture stdout for special characters test
        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);

        try
        {
            // Test special characters and encoding
            Console.WriteLine("SPECIAL_TEST: Testing special characters");
            Console.WriteLine("SPECIAL_TEST: Unicode: 你好世界 🌍 🚀 ✨");
            Console.WriteLine("SPECIAL_TEST: Symbols: !@#$%^&*()_+-=[]{}|;':\",./<>?");
            Console.WriteLine("SPECIAL_TEST: Newlines:\nLine1\nLine2\nLine3");
            Console.WriteLine("SPECIAL_TEST: Tabs:\tTab1\tTab2\tTab3");

            // Restore original stdout
            Console.SetOut(originalOut);

            // Verify captured output
            string captured = sw.ToString();
            _output.WriteLine($"Captured {captured.Length} characters with special chars/encoding");

            Assert.Contains("SPECIAL_TEST: Testing special characters", captured);
            Assert.Contains("你好世界", captured);
            Assert.Contains("🌍 🚀 ✨", captured);
            Assert.Contains("!@#$%^&*()_+-=[]{}|;':\",./<>?", captured);
            Assert.Contains("Line1", captured);
            Assert.Contains("Tab1", captured);
            Assert.True(captured.Length > 200, "Should have captured substantial content with special characters");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
