using System.Diagnostics;
using FluentAssertions;
using WebSearch.Providers;

namespace Hazina.Tools.Services.WebSearch.Providers.Tests;

/// <summary>
/// Live integration tests for <see cref="FreeWebSearchProvider"/>. These spawn a real `node`
/// process and require the one-time `npm ci` setup documented in
/// scripts/free-web-search/README.md to have been run. They are skipped (not failed) when:
/// - the SKIP_INTEGRATION environment variable is "1", or
/// - `node` is not on PATH, or
/// - the bundled script's node_modules/free-web-search dependency has not been installed.
///
/// This mirrors the early-return skip pattern used elsewhere in the repo for tests that
/// depend on an external/live resource (see Hazina.LLMs.OpenAI.Tests.ContinuationHooksIntegrationTests).
/// </summary>
[Trait("Category", "Integration")]
public class FreeWebSearchProviderIntegrationTests
{
    [Fact]
    public async Task SearchAsync_LiveQuery_ReturnsAtLeastOneHttpResult()
    {
        if (Environment.GetEnvironmentVariable("SKIP_INTEGRATION") == "1")
        {
            Console.WriteLine("Skipping: SKIP_INTEGRATION=1");
            return;
        }

        if (!IsNodeOnPath())
        {
            Console.WriteLine("Skipping: node is not on PATH");
            return;
        }

        var provider = new FreeWebSearchProvider();

        if (!await provider.IsAvailableAsync() || !HasFreeWebSearchDependencyInstalled())
        {
            Console.WriteLine(
                "Skipping: bundled script unavailable or `npm ci` has not been run in scripts/free-web-search " +
                "(see scripts/free-web-search/README.md).");
            return;
        }

        var results = await provider.SearchAsync("hazina framework");

        results.Should().NotBeEmpty();
        results[0].Url.Should().StartWith("http");
    }

    private static bool IsNodeOnPath()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                ArgumentList = { "--version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            process?.WaitForExit(5000);
            return process is { ExitCode: 0 };
        }
        catch
        {
            return false;
        }
    }

    private static bool HasFreeWebSearchDependencyInstalled()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 15 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "scripts", "free-web-search", "node_modules", "free-web-search");
            if (Directory.Exists(candidate))
                return true;
        }

        return false;
    }
}
