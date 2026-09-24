using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hazina.Security.ApiKeys;

/// <summary>The key source could not be reached and no (stale) cached record could stand in for it.</summary>
public sealed class ApiKeyLookupUnavailableException : Exception
{
    public ApiKeyLookupUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// The "hybrid" (option C) heart of the module. Keys are validated locally against a hash-keyed cache;
/// a miss or expired entry refreshes from the registered source (<see cref="IApiKeyLookup"/>: the app's
/// own table, or IAM via <see cref="HttpApiKeyLookup"/>). So:
/// <list type="bullet">
/// <item>the hot path makes no network hop;</item>
/// <item>a revocation made elsewhere takes effect within <see cref="HazinaApiKeyOptions.CachePositiveTtl"/> (minutes), or immediately in-process via <see cref="IApiKeyCache"/>;</item>
/// <item>if the source is down, previously validated keys keep working for <see cref="HazinaApiKeyOptions.CacheMaxStaleOnError"/>, so an IAM outage is not an outage of every app.</item>
/// </list>
/// Registered as a singleton; the source is resolved from a fresh DI scope per refresh so scoped
/// sources (an EF DbContext) work.
/// </summary>
public sealed class CachingApiKeyLookup : IApiKeyLookup, IApiKeyCache
{
    private const int MaxEntries = 10_000;
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(10);

    private sealed record Entry(ApiKeyRecord? Record, DateTimeOffset FreshUntil, DateTimeOffset StaleUntil);

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<HazinaApiKeyOptions> _options;
    private readonly ILogger<CachingApiKeyLookup> _logger;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<ApiKeyRecord?>> _inflight = new(StringComparer.Ordinal);

    public CachingApiKeyLookup(
        IServiceScopeFactory scopes,
        IOptions<HazinaApiKeyOptions> options,
        ILogger<CachingApiKeyLookup> logger,
        TimeProvider? time = null)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public async Task<ApiKeyRecord?> FindByHashAsync(string keyHash, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow();
        _entries.TryGetValue(keyHash, out var cached);
        if (cached is not null && now < cached.FreshUntil)
            return cached.Record;

        Task<ApiKeyRecord?>? load = null;
        try
        {
            // Single-flight per hash: a burst of requests with the same (possibly bogus) key costs one source call.
            load = _inflight.GetOrAdd(keyHash, static (hash, self) => self.LoadAsync(hash), this);
            return await load.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (cached?.Record is not null && now < cached.StaleUntil)
            {
                _logger.LogWarning(ex,
                    "API key source unavailable; serving cached key {KeyPrefix} (stale until {StaleUntil:O})",
                    cached.Record.KeyPrefix, cached.StaleUntil);
                // Back off so an outage is not met with one failing source call per request.
                _entries[keyHash] = cached with { FreshUntil = now + FailureBackoff };
                return cached.Record;
            }

            throw new ApiKeyLookupUnavailableException("The API key source is unavailable and no cached key can stand in.", ex);
        }
        finally
        {
            // Remove only OUR task: a completed load must not linger and answer later requests.
            if (load is not null)
                _inflight.TryRemove(new KeyValuePair<string, Task<ApiKeyRecord?>>(keyHash, load));
        }
    }

    private async Task<ApiKeyRecord?> LoadAsync(string keyHash)
    {
        using var scope = _scopes.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<IApiKeyLookup>();
        var record = await source.FindByHashAsync(keyHash, CancellationToken.None).ConfigureAwait(false);

        var opts = _options.Value;
        var now = _time.GetUtcNow();
        var ttl = record is null ? opts.CacheNegativeTtl : opts.CachePositiveTtl;
        var stale = record is null ? TimeSpan.Zero : opts.CacheMaxStaleOnError;

        if (_entries.Count >= MaxEntries)
            Trim(now);
        _entries[keyHash] = new Entry(record, now + ttl, now + ttl + stale);
        return record;
    }

    private void Trim(DateTimeOffset now)
    {
        foreach (var (hash, entry) in _entries)
            if (now >= entry.StaleUntil)
                _entries.TryRemove(hash, out _);

        // Still full (e.g. a flood of unique bad keys inside their TTL): drop everything rather than grow without bound.
        if (_entries.Count >= MaxEntries)
            _entries.Clear();
    }

    public void Invalidate(string keyHash) => _entries.TryRemove(keyHash, out _);

    public void InvalidateAll() => _entries.Clear();
}
