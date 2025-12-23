using System.Diagnostics;
using System.Text;

public static class ClaudeCliTool
{
    public static DevGPTChatTool Create()
    {
        var promptParam = new ChatToolParameter { Name = "prompt", Description = "The message to send to Claude.", Type = "string", Required = true };
        var modelParam = new ChatToolParameter { Name = "model", Description = "Optional model override (e.g., claude-3-5-sonnet-latest).", Type = "string", Required = false };
        var extraArgsParam = new ChatToolParameter { Name = "extra_args", Description = "Optional extra CLI args passed verbatim before the prompt.", Type = "string", Required = false };
        var timeoutParam = new ChatToolParameter { Name = "timeout", Description = "Optional timeout in seconds.", Type = "number", Required = false };

        return new DevGPTChatTool(
            name: "claude_cli",
            description: "Calls the Anthropic Claude CLI (requires 'claude' in PATH and logged-in) and returns combined stdout/stderr.",
            parameters: new List<ChatToolParameter> { promptParam, modelParam, extraArgsParam, timeoutParam },
            execute: async (messages, toolCall, cancel) => await DevGPTChatTool.CallTool(async () =>
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

                // Default timeout 120s if not specified
                int timeoutSeconds = 120;
                if (timeoutParam.TryGetValue(toolCall, out string timeoutStr) && int.TryParse(timeoutStr, out var t))
                {
                    timeoutSeconds = Math.Max(1, t);
                }

                return await RunProcessAsync(
                    arguments: argsBuilder.ToString().Trim(),
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

    private static ProcessStartInfo BuildClaudePsi(string arguments)
    {
        var envPath = Environment.GetEnvironmentVariable("CLAUDE_CLI_PATH");
        var isWindows = OperatingSystem.IsWindows();

        var psi = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

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
            psi.Arguments = $"/c claude {arguments}";
        }
        else
        {
            psi.FileName = "claude";
            psi.Arguments = arguments;
        }
        return psi;
    }

    private static async Task<string> RunProcessAsync(string arguments, TimeSpan timeout, CancellationToken cancel)
    {
        var psi = BuildClaudePsi(arguments);

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var tcs = new TaskCompletionSource<object?>();

        proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        try
        {
            if (!proc.Start())
                return "Failed to start 'claude' process.";
        }
        catch (Exception ex)
        {
            return $"Failed to start 'claude' process. Error: {ex.Message}\n" +
                   "Ensure the Claude CLI is installed, on PATH, and logged in. " +
                   "Optionally set full path via CLAUDE_CLI_PATH environment variable.";
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
