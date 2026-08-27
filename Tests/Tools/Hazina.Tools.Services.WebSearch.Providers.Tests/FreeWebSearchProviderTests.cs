using FluentAssertions;
using WebSearch.Core;
using WebSearch.Providers;

namespace Hazina.Tools.Services.WebSearch.Providers.Tests;

/// <summary>
/// Unit tests for <see cref="FreeWebSearchProvider"/>. These tests never spawn a real `node`
/// process - they substitute a fake <see cref="FreeWebSearchProvider.ProcessRunner"/> so they
/// run without any network access or a `node` install.
/// </summary>
public class FreeWebSearchProviderTests
{
    private const string FakeScriptPath = "fake-script.cjs";

    [Fact]
    public void GetProviderName_ReturnsFreeWebSearch()
    {
        var provider = new FreeWebSearchProvider("node", FakeScriptPath);

        provider.GetProviderName().Should().Be("FreeWebSearch");
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ThrowsArgumentException()
    {
        var provider = CreateProvider((_, _, _, _) =>
            Task.FromResult(new FreeWebSearchProvider.ProcessOutcome(0, "[]", string.Empty)));

        var act = async () => await provider.SearchAsync("   ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SearchAsync_CannedJson_ReturnsOrderedResultsWithSequentialRank()
    {
        const string json = """
        {
            "provider": "duckduckgo",
            "query": "hazina framework",
            "results": [
                {"title": "First Result", "url": "https://example.com/1", "snippet": "One"},
                {"title": "Second Result", "url": "https://example.com/2", "snippet": "Two"},
                {"title": "Third Result", "url": "https://example.com/3", "snippet": "Three"}
            ],
            "totalResults": 3
        }
        """;

        var provider = CreateProvider((_, _, _, _) =>
            Task.FromResult(new FreeWebSearchProvider.ProcessOutcome(0, json, string.Empty)));

        var results = await provider.SearchAsync("hazina framework");

        results.Should().HaveCount(3);
        results[0].Rank.Should().Be(1);
        results[1].Rank.Should().Be(2);
        results[2].Rank.Should().Be(3);
        results[0].Title.Should().Be("First Result");
        results[0].Url.Should().Be("https://example.com/1");
        results[0].Source.Should().Be("FreeWebSearch");
        results.Select(r => r.Url).Should().ContainInOrder(
            "https://example.com/1", "https://example.com/2", "https://example.com/3");
    }

    [Fact]
    public async Task SearchAsync_NonZeroExitCode_ThrowsHttpRequestExceptionContainingStderr()
    {
        const string stderrMessage = "Error: All search providers failed:\nDuckDuckGo: net::ERR_BLOCKED_BY_CLIENT";

        var provider = CreateProvider((_, _, _, _) =>
            Task.FromResult(new FreeWebSearchProvider.ProcessOutcome(1, string.Empty, stderrMessage)));

        var act = async () => await provider.SearchAsync("hazina framework");

        (await act.Should().ThrowAsync<HttpRequestException>())
            .Which.Message.Should().Contain(stderrMessage);
    }

    [Fact]
    public async Task IsAvailableAsync_ScriptPathMissing_ReturnsFalseWithoutThrowing()
    {
        var provider = new FreeWebSearchProvider(
            "node",
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "does-not-exist.cjs"));

        var available = await provider.IsAvailableAsync();

        available.Should().BeFalse();
    }

    private static FreeWebSearchProvider CreateProvider(FreeWebSearchProvider.ProcessRunner processRunner)
    {
        return new FreeWebSearchProvider("node", FakeScriptPath, TimeSpan.FromSeconds(30), processRunner);
    }
}
