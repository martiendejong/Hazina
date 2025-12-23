using System.Diagnostics;
using System.Text;

public static class ClaudeCodeRunner
{
    public static async Task<string> RunClaudeCodeAsync(
        string prompt,
        string? workingDirectory = null,
        string? model = null,
        string? extraArgs = null,
        int timeoutSeconds = 300,
        CancellationToken cancel = default)
    {
        var argsBuilder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(model)) argsBuilder.Append($"--model \"{EscapeArg(model)}\" ");
        if (!string.IsNullOrWhiteSpace(extraArgs)) argsBuilder.Append(extraArgs.Trim()).Append(' ');
        argsBuilder.Append($"\"{EscapeArg(prompt)}\" ");

        return await RunProcessAsync(
            argsBuilder.ToString().Trim(),
            workingDirectory,
            TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)),
            cancel);
    }

    private static string EscapeArg(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static ProcessStartInfo BuildClaudeCodePsi(string arguments, string? workingDirectory)
    {
        var envPath = Environment.GetEnvironmentVariable("CLAUDE_CODE_CLI_PATH");
        var isWindows = OperatingSystem.IsWindows();

        var psi = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }

        if (!string.IsNullOrWhiteSpace(envPath))
        {
            if (isWindows && (envPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || envPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)))
            {
                psi.FileName = "cmd.exe";
                psi.Arguments = $"/c \"\"{envPath}\" {arguments}\"";
            }
            else
            {
                psi.FileName = envPath;
                psi.Arguments = arguments;
            }
            return psi;
        }

        if (isWindows)
        {
            psi.FileName = "cmd.exe";
            psi.Arguments = $"/c claude-code {arguments}";
        }
        else
        {
            psi.FileName = "claude-code";
            psi.Arguments = arguments;
        }

        return psi;
    }

    private static async Task<string> RunProcessAsync(string arguments, string? workingDirectory, TimeSpan timeout, CancellationToken cancel)
    {
        var psi = BuildClaudeCodePsi(arguments, workingDirectory);

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        try
        {
            if (!proc.Start())
                return "Failed to start 'claude-code' process.";
        }
        catch (Exception ex)
        {
            return $"Failed to start 'claude-code' process. Error: {ex.Message}\n" +
                   "Ensure the Claude Code CLI is installed, on PATH, and logged in. " +
                   "Optionally set full path via CLAUDE_CODE_CLI_PATH environment variable.";
        }

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        cts.CancelAfter(timeout);

        await Task.WhenAny(WaitForExitAsync(proc, cts.Token));

        if (!proc.HasExited)
        {
            try { proc.Kill(true); } catch { }
            return $"Process timed out after {timeout.TotalSeconds} seconds.\n" + stdout.ToString().TrimEnd() + (stderr.Length > 0 ? "\n[stderr]\n" + stderr.ToString().TrimEnd() : "");
        }

        return stdout.ToString().TrimEnd() + (stderr.Length > 0 ? "\n[stderr]\n" + stderr.ToString().TrimEnd() : "");
    }

    private static Task WaitForExitAsync(Process proc, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<object?>();
        void Handler(object? s, EventArgs e)
        {
            tcs.TrySetResult(null);
            try { proc.Exited -= Handler; } catch { }
        }
        proc.Exited += Handler;
        if (proc.HasExited) Handler(proc, EventArgs.Empty);
        token.Register(() => tcs.TrySetCanceled(token));
        return tcs.Task;
    }
}
