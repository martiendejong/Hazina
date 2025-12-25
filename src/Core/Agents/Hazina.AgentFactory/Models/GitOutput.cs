// See https://aka.ms/new-console-template for more information
using System;
using System.Diagnostics;
using System.Text;

public static class GitOutput
{
    public static async Task<Tuple<string, string>> GetGitOutput(string workingDirectory, string arguments, TimeSpan timeout)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false, // Required to redirect output
            CreateNoWindow = true // Optional: prevent console window
        };

        using (var process = new Process { StartInfo = psi, EnableRaisingEvents = true })
        {
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Create timeout task
            var timeoutTask = Task.Delay(timeout);
            var processTask = Task.Run(() => process.WaitForExit());

            var completedTask = await Task.WhenAny(timeoutTask, processTask);

            if (completedTask == timeoutTask)
            {
                try
                {
                    process.Kill(true); // true = kill child processes too (e.g., npm -> node)
                }
                catch (Exception ex)
                {
                    errorBuilder.AppendLine("Error killing process: " + ex.Message);
                }

                throw new TimeoutException($"Process exceeded timeout of {timeout.TotalSeconds} seconds.");
            }

            return Tuple.Create(outputBuilder.ToString(), errorBuilder.ToString());
        }
    }
}

public static class NpmOutput
{
    public static async Task<Tuple<string, string>> GetNpmOutputAsync(string workingDirectory, string arguments, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "C:\\Program Files\\nodejs\\npm.cmd",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = psi, EnableRaisingEvents = true })
        {
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Create timeout task
            var timeoutTask = Task.Delay(timeout);
            var processTask = Task.Run(() => process.WaitForExit());

            var completedTask = await Task.WhenAny(timeoutTask, processTask);

            if (completedTask == timeoutTask)
            {
                try
                {
                    process.Kill(true); // true = kill child processes too (e.g., npm -> node)
                }
                catch (Exception ex)
                {
                    errorBuilder.AppendLine("Error killing process: " + ex.Message);
                }

                throw new TimeoutException($"Process exceeded timeout of {timeout.TotalSeconds} seconds.");
            }

            return Tuple.Create(outputBuilder.ToString(), errorBuilder.ToString());
        }
    }
}


public static class DotNetOutput
{
    public async static Task<Tuple<string, string>> GetDotNetOutput(string workingDirectory, string arguments, TimeSpan timeout)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false, // Required to redirect output
            CreateNoWindow = true // Optional: prevent console window
        };

        using (var process = new Process { StartInfo = psi, EnableRaisingEvents = true })
        {
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Create timeout task
            var timeoutTask = Task.Delay(timeout);
            var processTask = Task.Run(() => process.WaitForExit());

            var completedTask = await Task.WhenAny(timeoutTask, processTask);

            if (completedTask == timeoutTask)
            {
                try
                {
                    process.Kill(true); // true = kill child processes too (e.g., npm -> node)
                }
                catch (Exception ex)
                {
                    errorBuilder.AppendLine("Error killing process: " + ex.Message);
                }

                throw new TimeoutException($"Process exceeded timeout of {timeout.TotalSeconds} seconds.");
            }

            return Tuple.Create(outputBuilder.ToString(), errorBuilder.ToString());
        }
    }
}
