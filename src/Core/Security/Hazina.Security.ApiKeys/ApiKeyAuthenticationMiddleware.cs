using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hazina.Security.ApiKeys;

/// <summary>
/// Authenticates requests carrying the API-key header. No header: passes through untouched (JWT/cookie
/// auth carries on). Header present but bad: 401 right here, so no other scheme gets a second chance.
/// Valid key: builds the principal (scope / tenant / user / permissions), records the key for the rate
/// limiter and audits the request once the final status is known.
/// </summary>
public sealed class ApiKeyAuthenticationMiddleware
{
    private static readonly TimeSpan UsageRecordInterval = TimeSpan.FromMinutes(5);

    private readonly RequestDelegate _next;
    private readonly IOptions<HazinaApiKeyOptions> _options;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastUseRecorded = new(StringComparer.Ordinal);

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        IOptions<HazinaApiKeyOptions> options,
        ILogger<ApiKeyAuthenticationMiddleware> logger,
        TimeProvider? time = null)
    {
        _next = next;
        _options = options;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IApiKeyValidator validator,
        IEnumerable<IApiKeyAuditSink> auditSinks,
        IEnumerable<IApiKeyUsageRecorder> usageRecorders)
    {
        var options = _options.Value;

        if (IsExcluded(context.Request.Path, options)
            || !context.Request.Headers.TryGetValue(options.HeaderName, out var header))
        {
            await _next(context);
            return;
        }

        // Several header values are ambiguous: reject rather than guess which one counts.
        var rawKey = header.Count == 1 ? header.ToString() : null;
        var remoteIp = context.Connection.RemoteIpAddress;

        ApiKeyValidationResult result;
        try
        {
            result = await validator.ValidateAsync(rawKey, remoteIp, context.RequestAborted);
        }
        catch (ApiKeyLookupUnavailableException ex)
        {
            _logger.LogError(ex, "API key validation unavailable");
            context.Response.Headers.RetryAfter = "5";
            await RejectAsync(context, StatusCodes.Status503ServiceUnavailable, "ServiceUnavailable",
                "API key validation is temporarily unavailable.", ApiKeyAuditOutcome.LookupUnavailable, null, rawKey, auditSinks);
            return;
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Rejected API key ({Outcome}) from {IpAddress}", result.Outcome, remoteIp);
            var (status, error, message) = result.Outcome switch
            {
                ApiKeyAuditOutcome.EmptyKey => (StatusCodes.Status401Unauthorized, "Unauthorized", "API key header is empty."),
                ApiKeyAuditOutcome.IpNotAllowed => (StatusCodes.Status403Forbidden, "Forbidden", "IP address not allowed for this API key."),
                // Revoked / expired / unknown look identical to the caller; only the audit trail tells them apart.
                _ => (StatusCodes.Status401Unauthorized, "Unauthorized", "Invalid or expired API key."),
            };
            await RejectAsync(context, status, error, message, result.Outcome, result.Key, rawKey, auditSinks);
            return;
        }

        var key = result.Key!;

        if (options.EnforceTenantHints && !TenantHintsAllowed(context, key, options))
        {
            _logger.LogWarning("API key {KeyPrefix} addressed a tenant it does not belong to", key.KeyPrefix);
            await RejectAsync(context, StatusCodes.Status403Forbidden, "Forbidden",
                "This API key is not valid for the requested tenant.", ApiKeyAuditOutcome.TenantMismatch, key, rawKey, auditSinks);
            return;
        }

        context.User = ApiKeyPrincipal.Create(key);
        context.Items[ApiKeyDefaults.RecordItemKey] = key;
        context.Items[ApiKeyDefaults.PrefixItemKey] = key.KeyPrefix;

        await RecordUseAsync(key, usageRecorders);

        var status2 = StatusCodes.Status500InternalServerError;
        try
        {
            await _next(context);
            status2 = context.Response.StatusCode;
        }
        finally
        {
            var outcome = context.Items[ApiKeyDefaults.AuditOutcomeItemKey] is ApiKeyAuditOutcome marked
                ? marked
                : status2 switch
                {
                    StatusCodes.Status429TooManyRequests => ApiKeyAuditOutcome.RateLimited,
                    StatusCodes.Status403Forbidden => ApiKeyAuditOutcome.Forbidden,
                    _ => ApiKeyAuditOutcome.Authenticated,
                };
            await AuditAsync(auditSinks, Entry(context, outcome, key, rawKey, status2));
        }
    }

    private static bool IsExcluded(PathString path, HazinaApiKeyOptions options) =>
        options.ExcludedPathPrefixes
            .Select(prefix => prefix.TrimEnd('/'))
            .Where(prefix => prefix.Length > 0)
            .Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool TenantHintsAllowed(HttpContext context, ApiKeyRecord key, HazinaApiKeyOptions options)
    {
        var hinted = new List<string>();
        foreach (var h in context.Request.Headers[options.TenantHeaderName])
            if (!string.IsNullOrWhiteSpace(h)) hinted.Add(h!);
        foreach (var name in options.TenantParameterNames)
            foreach (var q in context.Request.Query[name])
                if (!string.IsNullOrWhiteSpace(q)) hinted.Add(q!);

        if (hinted.Count == 0) return true;

        // Same rule as ClaimsPrincipal.CanAccessTenant, evaluated on the record (the principal is not built yet).
        var principal = ApiKeyPrincipal.Create(key);
        return hinted.All(principal.CanAccessTenant);
    }

    private async Task RecordUseAsync(ApiKeyRecord key, IEnumerable<IApiKeyUsageRecorder> recorders)
    {
        var now = _time.GetUtcNow();
        if (_lastUseRecorded.TryGetValue(key.Id, out var last) && now - last < UsageRecordInterval)
            return;
        _lastUseRecorded[key.Id] = now;

        foreach (var recorder in recorders)
        {
            try { await recorder.RecordUseAsync(key, now, CancellationToken.None); }
            catch (Exception ex) { _logger.LogWarning(ex, "Recording API key use failed for {KeyPrefix}", key.KeyPrefix); }
        }
    }

    private async Task RejectAsync(
        HttpContext context, int status, string error, string message,
        ApiKeyAuditOutcome outcome, ApiKeyRecord? key, string? rawKey, IEnumerable<IApiKeyAuditSink> sinks)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync($$"""{"error":"{{error}}","message":"{{message}}"}""");
        await AuditAsync(sinks, Entry(context, outcome, key, rawKey, status));
    }

    private ApiKeyAuditEntry Entry(HttpContext context, ApiKeyAuditOutcome outcome, ApiKeyRecord? key, string? rawKey, int status)
    {
        // For unknown keys the prefix is only reported when the value has the shape of a key we mint (see TryExtractPrefix).
        var prefix = key?.KeyPrefix ?? (ApiKeyGenerator.TryExtractPrefix(rawKey, out var p) ? p : null);
        var endpoint = context.GetEndpoint() is RouteEndpoint route && route.RoutePattern.RawText is { } pattern
            ? pattern
            : context.Request.Path.Value ?? "/";

        return new ApiKeyAuditEntry(
            _time.GetUtcNow(), outcome, prefix, key?.Id, key?.TenantId, key?.Scope.ToClaimValue(),
            context.Request.Method, endpoint, status, context.Connection.RemoteIpAddress?.ToString());
    }

    private async Task AuditAsync(IEnumerable<IApiKeyAuditSink> sinks, ApiKeyAuditEntry entry)
    {
        foreach (var sink in sinks)
        {
            try { await sink.WriteAsync(entry, CancellationToken.None); }
            catch (Exception ex) { _logger.LogError(ex, "API key audit sink {Sink} failed", sink.GetType().Name); }
        }
    }
}
