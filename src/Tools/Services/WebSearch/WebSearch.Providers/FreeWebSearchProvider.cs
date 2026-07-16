using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using WebSearch.Core;

namespace WebSearch.Providers;

/// <summary>
/// Search provider that wraps the `free-web-search` npm package (a Puppeteer-based Google
/// scraper) by spawning a bundled Node.js script. Intended as a CAPTCHA fallback for
/// <see cref="GoogleSearchProvider"/> when Google starts returning a challenge page.
/// </summary>
public class FreeWebSearchProvider : ISearchProvider
{
    /// <summary>
    /// Runs an external process and captures its outcome. Exposed so tests can substitute a
    /// fake process runner without spawning a real OS process or requiring `node` to be
    /// installed on the test machine.
    /// </summary>
    public delegate Task<ProcessOutcome> ProcessRunner(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// Result of running an external process via <see cref="ProcessRunner"/>.
    /// </summary>
    public readonly record struct ProcessOutcome(int ExitCode, string StandardOutput, string StandardError);

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan AvailabilityCheckTimeout = TimeSpan.FromSeconds(5);

    private readonly string _nodeExecutable;
    private readonly string _scriptPath;
    private readonly TimeSpan _timeout;
    private readonly ProcessRunner _processRunner;

    /// <summary>
    /// Creates a provider that resolves `node` from PATH and locates the bundled
    /// scripts/free-web-search/run.cjs script relative to the repo root.
    /// </summary>
    public FreeWebSearchProvider()
        : this("node", ResolveDefaultScriptPath(), DefaultTimeout)
    {
    }

    /// <summary>
    /// Creates a provider with an explicit node executable and script path.
    /// </summary>
    public FreeWebSearchProvider(string nodeExecutable, string scriptPath, TimeSpan? timeout = null)
        : this(nodeExecutable, scriptPath, timeout, RunProcessAsync)
    {
    }

    /// <summary>
    /// Full constructor allowing the process runner to be substituted. Intended for tests, so
    /// the "3 canned results" / "non-zero exit code" scenarios can be exercised without
    /// spawning a real `node` process.
    /// </summary>
    public FreeWebSearchProvider(string nodeExecutable, string scriptPath, TimeSpan? timeout, ProcessRunner processRunner)
    {
        _nodeExecutable = nodeExecutable;
        _scriptPath = scriptPath;
        _timeout = timeout ?? DefaultTimeout;
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public string GetProviderName() => "FreeWebSearch";

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_scriptPath) || !File.Exists(_scriptPath))
            return false;

        try
        {
            var outcome = await _processRunner(_nodeExecutable, new[] { "--version" }, AvailabilityCheckTimeout, cancellationToken);
            return outcome.ExitCode == 0;
        }
        catch
        {
            // node missing from PATH, script missing, or the check timed out/failed for any reason.
            return false;
        }
    }

    public async Task<SearchResult[]> SearchAsync(
        string query,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        options ??= new SearchOptions();

        // The underlying free-web-search package has no safe-search knob; SafeSearch is
        // accepted for interface compatibility but is otherwise a no-op passthrough here.
        var arguments = new[]
        {
            _scriptPath,
            query,
            options.MaxResults.ToString(CultureInfo.InvariantCulture),
            options.Language ?? "en"
        };

        var outcome = await _processRunner(_nodeExecutable, arguments, _timeout, cancellationToken);

        if (outcome.ExitCode != 0)
        {
            throw new HttpRequestException(
                $"free-web-search script '{_scriptPath}' exited with code {outcome.ExitCode}. " +
                $"stderr: {outcome.StandardError.Trim()}");
        }

        var results = ParseResults(outcome.StandardOutput, GetProviderName());
        return results.Take(options.MaxResults).ToArray();
    }

    /// <summary>
    /// Default <see cref="ProcessRunner"/> used in production: spawns a real OS process,
    /// captures stdout/stderr, and enforces <paramref name="timeout"/> by killing the process
    /// tree and throwing <see cref="TimeoutException"/> if it doesn't exit in time.
    /// </summary>
    private static async Task<ProcessOutcome> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
            startInfo.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            TryKill(process);
            throw;
        }

        return new ProcessOutcome(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static List<SearchResult> ParseResults(string json, string providerName)
    {
        var results = new List<SearchResult>();

        if (string.IsNullOrWhiteSpace(json))
            return results;

        ScriptResult[]? scriptResults;
        try
        {
            scriptResults = JsonSerializer.Deserialize<ScriptResult[]>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new HttpRequestException(
                $"free-web-search script produced invalid JSON output: {ex.Message}");
        }

        if (scriptResults == null)
            return results;

        var rank = 1;
        foreach (var item in scriptResults)
        {
            if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Url))
                continue;

            results.Add(new SearchResult
            {
                Title = item.Title,
                Url = item.Url,
                Snippet = item.Snippet,
                Source = providerName,
                Rank = rank++,
                FetchedAt = DateTime.UtcNow
            });
        }

        return results;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort cleanup; nothing more we can do if the kill itself fails.
        }
    }

    private static string ResolveDefaultScriptPath()
    {
        const string relativePath = "scripts/free-web-search/run.cjs";

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 15 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "scripts", "free-web-search", "run.cjs");
            if (File.Exists(candidate))
                return candidate;
        }

        // Not found: return the conventional path anyway so IsAvailableAsync can report
        // unavailable (via File.Exists) rather than crashing on construction.
        return Path.Combine(AppContext.BaseDirectory, relativePath);
    }

    private sealed class ScriptResult
    {
        public string? Title { get; set; }
        public string? Url { get; set; }
        public string? Snippet { get; set; }
    }
}
