using System.Diagnostics;
using System.Text;

public static class ClaudeCodeTool
{
    public static HazinaChatTool Create()
    {
        var promptParam = new ChatToolParameter { Name = "prompt", Description = "The coding task to send to Claude Code.", Type = "string", Required = true };
        var workingDirParam = new ChatToolParameter { Name = "working_dir", Description = "Optional working directory path for the code task.", Type = "string", Required = false };
        var modelParam = new ChatToolParameter { Name = "model", Description = "Optional model override (e.g., claude-sonnet-4-5-20250929).", Type = "string", Required = false };
        var extraArgsParam = new ChatToolParameter { Name = "extra_args", Description = "Optional extra CLI args passed verbatim before the prompt.", Type = "string", Required = false };
        var timeoutParam = new ChatToolParameter { Name = "timeout", Description = "Optional timeout in seconds (default 300).", Type = "number", Required = false };

        return new HazinaChatTool(
            name: "claude_code",
            description: "Calls the Claude Code CLI (requires 'claude-code' in PATH and logged-in) for AI-assisted coding tasks. Returns combined stdout/stderr. Use this to have Claude Code write, modify, or analyze code in a specific directory.",
            parameters: new List<ChatToolParameter> { promptParam, workingDirParam, modelParam, extraArgsParam, timeoutParam },
            execute: async (messages, toolCall, cancel) => await HazinaChatTool.CallTool(async () =>
            {
                if (!promptParam.TryGetValue(toolCall, out string prompt))
                    return "No prompt provided";

                var argsBuilder = new StringBuilder();

                if (modelParam.TryGetValue(toolCall, out string model) && !string.IsNullOrWhiteSpace(model))
                {
                    argsBuilder.Append($"--model \"{EscapeArg(model)}\" ");
                }

                if (extraArgsParam.TryGetValue(toolCall, out string extra) && !string.IsNullOrWhiteSpace(extra))
                {
                    argsBuilder.Append(extra.Trim()).Append(' ');
                }

                argsBuilder.Append($"\"{EscapeArg(prompt)}\" ");

                // Default timeout 300s (5 min) for coding tasks
                int timeoutSeconds = 300;
                if (timeoutParam.TryGetValue(toolCall, out string timeoutStr) && int.TryParse(timeoutStr, out var t))
                {
                    timeoutSeconds = Math.Max(1, t);
                }

                string? workingDir = null;
                if (workingDirParam.TryGetValue(toolCall, out string dir) && !string.IsNullOrWhiteSpace(dir))
                {
                    workingDir = dir;
                }

                return await RunProcessAsync(
                    arguments: argsBuilder.ToString().Trim(),
                    workingDirectory: workingDir,
                    timeout: TimeSpan.FromSeconds(timeoutSeconds),
                    cancel: cancel);
            }, cancel)
        );
    }

    public static void Register(IToolsContext tools)
    {
        tools.Add(Create());
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
        var tcs = new TaskCompletionSource<object?>();

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

        await Task.WhenAny(tcs.Task, WaitForExitAsync(proc, cts.Token));

        if (!proc.HasExited)
        {
            try { proc.Kill(true); } catch { /* ignore */ }
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
        if (proc.HasExited)
        {
            Handler(proc, EventArgs.Empty);
        }
        token.Register(() => tcs.TrySetCanceled(token));
        return tcs.Task;
    }
}
