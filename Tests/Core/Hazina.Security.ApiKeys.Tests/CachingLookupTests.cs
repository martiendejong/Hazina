using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Hazina.Security.ApiKeys.Tests;

/// <summary>The hybrid (option C) behaviour: local validation, short-lived cache, refresh from the authority, survive its outage.</summary>
public class CachingLookupTests
{
    private sealed class CountingSource : IApiKeyLookup
    {
        public Dictionary<string, ApiKeyRecord> Keys { get; } = new();
        public int Calls;
        public Exception? Fail;
        public TaskCompletionSource? Gate;

        public async Task<ApiKeyRecord?> FindByHashAsync(string keyHash, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Calls);
            if (Gate is not null) await Gate.Task;
            if (Fail is not null) throw Fail;
            return Keys.TryGetValue(keyHash, out var k) ? k : null;
        }
    }

    private static (CachingApiKeyLookup Cache, CountingSource Source, FakeTimeProvider Time, ApiKeyValidator Validator) Create(
        Action<HazinaApiKeyOptions>? configure = null)
    {
        var source = new CountingSource();
        var services = new ServiceCollection();
        services.AddSingleton<IApiKeyLookup>(source);
        var provider = services.BuildServiceProvider();

        var options = new HazinaApiKeyOptions
        {
            CachePositiveTtl = TimeSpan.FromMinutes(2),
            CacheNegativeTtl = TimeSpan.FromSeconds(15),
            CacheMaxStaleOnError = TimeSpan.FromMinutes(15),
        };
        configure?.Invoke(options);

        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-24T10:00:00Z"));
        var cache = new CachingApiKeyLookup(
            provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(options),
            NullLogger<CachingApiKeyLookup>.Instance, time);
        return (cache, source, time, new ApiKeyValidator(cache, time));
    }

    private static (string Raw, ApiKeyRecord Record) NewKey(bool active = true)
    {
        var g = ApiKeyGenerator.Generate("tst");
        return (g.RawKey, new ApiKeyRecord { Id = Guid.NewGuid().ToString(), KeyHash = g.KeyHash, KeyPrefix = g.KeyPrefix, IsActive = active, TenantId = "t1" });
    }

    [Fact]
    public async Task HotPath_HitsTheSourceOnce_ThenServesLocally()
    {
        var (cache, source, _, validator) = Create();
        var (raw, record) = NewKey();
        source.Keys[record.KeyHash] = record;

        for (var i = 0; i < 10; i++)
            Assert.True((await validator.ValidateAsync(raw, null)).Succeeded);

        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task RevocationAtTheAuthority_PropagatesOnceTheTtlLapses()
    {
        var (cache, source, time, validator) = Create();
        var (raw, record) = NewKey();
        source.Keys[record.KeyHash] = record;
        Assert.True((await validator.ValidateAsync(raw, null)).Succeeded);

        // The authority (IAM) revokes the key.
        source.Keys[record.KeyHash] = new ApiKeyRecord { Id = record.Id, KeyHash = record.KeyHash, KeyPrefix = record.KeyPrefix, IsActive = false };

        // Within the TTL the cached decision stands (that is the trade for having no per-request hop)...
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.True((await validator.ValidateAsync(raw, null)).Succeeded);

        // ...and after it, the refreshed record rejects the key: revoked within minutes.
        time.Advance(TimeSpan.FromMinutes(2));
        var result = await validator.ValidateAsync(raw, null);
        Assert.Equal(ApiKeyAuditOutcome.Revoked, result.Outcome);
    }

    [Fact]
    public async Task Invalidate_TakesEffectImmediately()
    {
        var (cache, source, _, validator) = Create();
        var (raw, record) = NewKey();
        source.Keys[record.KeyHash] = record;
        Assert.True((await validator.ValidateAsync(raw, null)).Succeeded);

        source.Keys[record.KeyHash] = new ApiKeyRecord { Id = record.Id, KeyHash = record.KeyHash, KeyPrefix = record.KeyPrefix, IsActive = false };
        cache.Invalidate(record.KeyHash);

        Assert.Equal(ApiKeyAuditOutcome.Revoked, (await validator.ValidateAsync(raw, null)).Outcome);
    }

    [Fact]
    public async Task UnknownKeys_AreNegativelyCached_ThenRetriedAfterTheShortTtl()
    {
        var (cache, source, time, validator) = Create();
        var (raw, record) = NewKey();

        for (var i = 0; i < 5; i++)
            Assert.Equal(ApiKeyAuditOutcome.InvalidKey, (await validator.ValidateAsync(raw, null)).Outcome);
        Assert.Equal(1, source.Calls);

        // The key gets provisioned at the authority; the negative entry expires quickly so it is picked up.
        source.Keys[record.KeyHash] = record;
        time.Advance(TimeSpan.FromSeconds(20));
        Assert.True((await validator.ValidateAsync(raw, null)).Succeeded);
    }

    [Fact]
    public async Task SourceOutage_ServesTheLastKnownGoodRecord_UntilMaxStale()
    {
        var (cache, source, time, validator) = Create();
        var (raw, record) = NewKey();
        source.Keys[record.KeyHash] = record;
        Assert.True((await validator.ValidateAsync(raw, null)).Succeeded);

        source.Fail = new HttpRequestException("IAM is down");
        time.Advance(TimeSpan.FromMinutes(3)); // TTL over, stale window (15 min) still open

        Assert.True((await validator.ValidateAsync(raw, null)).Succeeded);

        time.Advance(TimeSpan.FromMinutes(20)); // stale window over
        await Assert.ThrowsAsync<ApiKeyLookupUnavailableException>(() => validator.ValidateAsync(raw, null));
    }

    [Fact]
    public async Task SourceOutage_DoesNotHammerTheSource()
    {
        var (cache, source, time, validator) = Create();
        var (raw, record) = NewKey();
        source.Keys[record.KeyHash] = record;
        await validator.ValidateAsync(raw, null);

        source.Fail = new HttpRequestException("IAM is down");
        time.Advance(TimeSpan.FromMinutes(3));
        var callsBefore = source.Calls;

        for (var i = 0; i < 50; i++)
            await validator.ValidateAsync(raw, null);

        Assert.Equal(callsBefore + 1, source.Calls); // one failed attempt, then a back-off window of cached answers
    }

    [Fact]
    public async Task SourceOutage_WithNothingCached_FailsClosed()
    {
        var (cache, source, _, validator) = Create();
        source.Fail = new HttpRequestException("IAM is down");
        var (raw, _) = NewKey();

        await Assert.ThrowsAsync<ApiKeyLookupUnavailableException>(() => validator.ValidateAsync(raw, null));
    }

    [Fact]
    public async Task ConcurrentMisses_ForTheSameKey_CostOneSourceCall()
    {
        var (cache, source, _, validator) = Create();
        var (raw, record) = NewKey();
        source.Keys[record.KeyHash] = record;
        source.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var pending = Enumerable.Range(0, 25).Select(_ => validator.ValidateAsync(raw, null)).ToList();
        await Task.Delay(100);
        source.Gate.SetResult();
        var results = await Task.WhenAll(pending);

        Assert.All(results, r => Assert.True(r.Succeeded));
        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task StaleServing_CanBeDisabled()
    {
        var (cache, source, time, validator) = Create(o => o.CacheMaxStaleOnError = TimeSpan.Zero);
        var (raw, record) = NewKey();
        source.Keys[record.KeyHash] = record;
        await validator.ValidateAsync(raw, null);

        source.Fail = new HttpRequestException("IAM is down");
        time.Advance(TimeSpan.FromMinutes(3));

        await Assert.ThrowsAsync<ApiKeyLookupUnavailableException>(() => validator.ValidateAsync(raw, null));
    }

    [Fact]
    public async Task Middleware_AnswersLookupOutageWith503_AndAudits()
    {
        var failing = new CountingSource { Fail = new HttpRequestException("IAM is down") };
        await using var app = await TestApp.StartAsync(services: s =>
        {
            s.AddSingleton<IApiKeyLookup>(failing); // the source behind the cache
        });
        var raw = ApiKeyGenerator.Generate("tst").RawKey;

        var response = await app.GetAsync("/read", raw);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.Contains("Retry-After"));
        Assert.Contains(app.Audit.Entries, e => e.Outcome == ApiKeyAuditOutcome.LookupUnavailable);
    }
}
