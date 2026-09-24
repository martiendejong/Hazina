using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hazina.Security.ApiKeys.Tests;

/// <summary>Collects audit entries in memory.</summary>
public sealed class CapturingAuditSink : IApiKeyAuditSink
{
    public List<ApiKeyAuditEntry> Entries { get; } = new();

    public Task WriteAsync(ApiKeyAuditEntry entry, CancellationToken ct = default)
    {
        lock (Entries) Entries.Add(entry);
        return Task.CompletedTask;
    }
}

/// <summary>Vault double: keeps the raw keys it is given (that is the point of the vault) and can be told to fail.</summary>
public sealed class FakeVault : IApiKeySecretVault
{
    private int _next = 1;
    public Dictionary<string, ApiKeySecretEntry> Secrets { get; } = new();
    public bool FailStore { get; set; }
    public List<string> Deleted { get; } = new();

    public Task<string> StoreAsync(ApiKeySecretEntry entry, CancellationToken ct = default)
    {
        if (FailStore) throw new HttpRequestException("vault down");
        var reference = entry.ExistingReference ?? $"cred-{_next++}";
        Secrets[reference] = entry;
        return Task.FromResult(reference);
    }

    public Task DeleteAsync(string reference, CancellationToken ct = default)
    {
        Secrets.Remove(reference);
        Deleted.Add(reference);
        return Task.CompletedTask;
    }
}

public sealed class LogCapture : ILoggerProvider
{
    public List<string> Lines { get; } = new();
    public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);
    public void Dispose() { }

    private sealed class CaptureLogger : ILogger
    {
        private readonly LogCapture _owner;
        public CaptureLogger(LogCapture owner) => _owner = owner;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_owner.Lines) _owner.Lines.Add(formatter(state, exception) + exception);
        }
    }
}

/// <summary>A real ASP.NET Core pipeline on TestServer with the API-key middleware wired exactly as an app would.</summary>
public sealed class TestApp : IAsyncDisposable
{
    public const string TenantA = "11111111-1111-1111-1111-111111111111";
    public const string TenantB = "22222222-2222-2222-2222-222222222222";

    private readonly WebApplication _app;

    public HttpClient Client { get; }
    public InMemoryApiKeyStore Store { get; }
    public CapturingAuditSink Audit { get; }
    public FakeVault Vault { get; }
    public LogCapture Logs { get; }
    public IServiceProvider Services => _app.Services;

    private TestApp(WebApplication app, CapturingAuditSink audit, FakeVault vault, LogCapture logs)
    {
        _app = app;
        Store = app.Services.GetRequiredService<InMemoryApiKeyStore>();
        Audit = audit;
        Vault = vault;
        Logs = logs;
        Client = app.GetTestClient();
    }

    public static async Task<TestApp> StartAsync(
        Action<HazinaApiKeyOptions>? configure = null,
        Action<IServiceCollection>? services = null,
        bool duplicateRateLimiter = false,
        bool useAuthentication = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var audit = new CapturingAuditSink();
        var vault = new FakeVault();
        var logs = new LogCapture();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);

        builder.Services.AddHazinaApiKeyAuth(configure).UseInMemoryStore();
        builder.Services.AddSingleton<IApiKeyAuditSink>(audit);
        builder.Services.AddSingleton<IApiKeySecretVault>(vault);
        builder.Services.AddScoped<IApiKeyManager, ApiKeyManager>();
        services?.Invoke(builder.Services);

        var app = builder.Build();
        app.UseHazinaApiKeyAuth();
        if (useAuthentication) app.UseAuthentication();
        if (duplicateRateLimiter) app.UseRateLimiter();
        app.UseAuthorization();

        app.MapGet("/open", () => "open");
        app.MapGet("/read", () => "read").RequireAuthorization(HazinaApiKeyPolicies.Read);
        app.MapPost("/write", () => "write").RequireAuthorization(HazinaApiKeyPolicies.Write);
        app.MapGet("/admin", () => "admin").RequireAuthorization(HazinaApiKeyPolicies.Admin);
        app.MapGet("/tenants/{tenantId}/data", (string tenantId) => $"data-{tenantId}").RequireAuthorization(HazinaApiKeyPolicies.Read);
        app.MapGet("/oauth/token", () => "oauth");
        app.MapGet("/whoami", (ClaimsPrincipal user) =>
            user.Claims.Select(c => new { c.Type, c.Value }).ToList()).RequireAuthorization(HazinaApiKeyPolicies.Read);

        await app.StartAsync();
        return new TestApp(app, audit, vault, logs);
    }

    /// <summary>Mint a key straight into the store (hash only) and return the raw key.</summary>
    public async Task<string> AddKeyAsync(
        ApiKeyScope scope = ApiKeyScope.Read,
        string? tenantId = TenantA,
        int? rateLimit = null,
        DateTimeOffset? expiresAt = null,
        bool active = true,
        IReadOnlyList<string>? allowedIps = null,
        string name = "test key",
        string? userId = null,
        IReadOnlyList<string>? permissions = null,
        IReadOnlyList<Claim>? extraClaims = null)
    {
        var generated = ApiKeyGenerator.Generate("tst");
        await Store.AddAsync(new ApiKeyRecord
        {
            Id = Guid.NewGuid().ToString("D"),
            KeyHash = generated.KeyHash,
            KeyPrefix = generated.KeyPrefix,
            Name = name,
            Scope = scope,
            TenantId = tenantId,
            UserId = userId,
            Permissions = permissions ?? Array.Empty<string>(),
            RateLimitPerMinute = rateLimit,
            ExpiresAtUtc = expiresAt,
            IsActive = active,
            AllowedIps = allowedIps ?? Array.Empty<string>(),
            ExtraClaims = extraClaims ?? Array.Empty<Claim>(),
        });
        return generated.RawKey;
    }

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? apiKey, Action<HttpRequestMessage>? tweak = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (apiKey is not null) request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        tweak?.Invoke(request);
        return Client.SendAsync(request);
    }

    public Task<HttpResponseMessage> GetAsync(string path, string? apiKey) => SendAsync(HttpMethod.Get, path, apiKey);

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}
