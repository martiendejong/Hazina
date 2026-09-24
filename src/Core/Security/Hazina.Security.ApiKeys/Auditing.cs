using Microsoft.Extensions.Logging;

namespace Hazina.Security.ApiKeys;

/// <summary>
/// Default audit sink: one structured log line per API-key request under the category
/// "Hazina.Security.ApiKeys.Audit". Route it to a dedicated log file/index in the app's logging config.
/// Carries the key PREFIX, tenant, scope, endpoint and timestamp; never the raw key.
/// </summary>
public sealed class LoggerApiKeyAuditSink : IApiKeyAuditSink
{
    private readonly ILogger _logger;

    public LoggerApiKeyAuditSink(ILoggerFactory loggerFactory) =>
        _logger = loggerFactory.CreateLogger("Hazina.Security.ApiKeys.Audit");

    public Task WriteAsync(ApiKeyAuditEntry e, CancellationToken ct = default)
    {
        var level = e.Outcome == ApiKeyAuditOutcome.Authenticated && e.StatusCode < 400
            ? LogLevel.Information
            : LogLevel.Warning;

        _logger.Log(level,
            "ApiKeyAudit {Timestamp:O} outcome={Outcome} keyPrefix={KeyPrefix} keyId={KeyId} tenant={TenantId} scope={Scope} request={Method} {Endpoint} status={StatusCode} ip={RemoteIp}",
            e.TimestampUtc, e.Outcome, e.KeyPrefix ?? "-", e.KeyId ?? "-", e.TenantId ?? "-", e.Scope ?? "-",
            e.Method, e.Endpoint, e.StatusCode, e.RemoteIp ?? "-");
        return Task.CompletedTask;
    }
}
