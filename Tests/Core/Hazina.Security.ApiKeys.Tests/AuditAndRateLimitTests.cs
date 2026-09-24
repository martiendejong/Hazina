using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.Security.ApiKeys.Tests;

public class AuditTests
{
    [Fact]
    public async Task EveryApiKeyRequest_IsAudited_WithPrefixTenantScopeEndpointAndTimestamp()
    {
        await using var app = await TestApp.StartAsync();
        var key = await app.AddKeyAsync(ApiKeyScope.Write, TestApp.TenantA);
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        await app.GetAsync($"/tenants/{TestApp.TenantA}/data", key);
        await app.SendAsync(HttpMethod.Post, "/write", key);
        await app.GetAsync("/admin", key); // 403: scope too low
        await app.GetAsync("/read", "tst_zzzz_" + new string('x', 43)); // well-formed but unknown

        var entries = app.Audit.Entries;
        Assert.Equal(4, entries.Count);

        var ok = entries[0];
        Assert.Equal(ApiKeyAuditOutcome.Authenticated, ok.Outcome);
        Assert.StartsWith("tst_", ok.KeyPrefix);
        Assert.Equal(TestApp.TenantA, ok.TenantId);
        Assert.Equal("write", ok.Scope);
        Assert.Equal("GET", ok.Method);
        Assert.Equal("/tenants/{tenantId}/data", ok.Endpoint); // route pattern, not the raw path
        Assert.Equal(200, ok.StatusCode);
        Assert.True(ok.TimestampUtc >= before);

        Assert.Equal("POST", entries[1].Method);
        Assert.Equal(ApiKeyAuditOutcome.Forbidden, entries[2].Outcome);
        Assert.Equal(403, entries[2].StatusCode);

        Assert.Equal(ApiKeyAuditOutcome.InvalidKey, entries[3].Outcome);
        Assert.Equal("tst_zzzz_", entries[3].KeyPrefix); // shaped like a key we mint, so its (non-secret) prefix is reported
        Assert.Null(entries[3].KeyId);
    }

    [Fact]
    public async Task RawKey_NeverAppearsInAuditEntriesLogsOrResponses()
    {
        await using var app = await TestApp.StartAsync();
        var good = await app.AddKeyAsync(ApiKeyScope.Read);
        var revoked = await app.AddKeyAsync(active: false);
        const string pastedSecret = "correct-horse-battery-staple-hunter2";

        var responses = new List<string>();
        foreach (var k in new[] { good, revoked, pastedSecret })
            responses.Add(await (await app.GetAsync("/read", k)).Content.ReadAsStringAsync());

        var everything = JsonSerializer.Serialize(app.Audit.Entries) + string.Join('\n', app.Logs.Lines) + string.Join('\n', responses);
        foreach (var secret in new[] { good, revoked, pastedSecret })
            Assert.DoesNotContain(secret, everything);
        Assert.DoesNotContain(good[^30..], everything); // nor any long fragment of it
    }

    [Fact]
    public async Task UnknownGarbageKey_ReportsNoPrefix()
    {
        await using var app = await TestApp.StartAsync();

        await app.GetAsync("/read", "hunter2-my-password");

        var entry = Assert.Single(app.Audit.Entries);
        Assert.Equal(ApiKeyAuditOutcome.InvalidKey, entry.Outcome);
        Assert.Null(entry.KeyPrefix); // never echo the first characters of an arbitrary secret
    }

    [Fact]
    public async Task FailingAuditSink_NeverFailsTheRequest()
    {
        await using var app = await TestApp.StartAsync(services: s => s.AddSingleton<IApiKeyAuditSink, ThrowingSink>());
        var key = await app.AddKeyAsync();

        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/read", key)).StatusCode);
    }

    private sealed class ThrowingSink : IApiKeyAuditSink
    {
        public Task WriteAsync(ApiKeyAuditEntry entry, CancellationToken ct = default) => throw new InvalidOperationException("sink down");
    }
}

public class RateLimitTests
{
    [Fact]
    public async Task PerKeyLimit_IsEnforced_WithRetryAfter_AndAudited()
    {
        await using var app = await TestApp.StartAsync();
        var limited = await app.AddKeyAsync(rateLimit: 3);
        var other = await app.AddKeyAsync(rateLimit: 3);

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/read", limited)).StatusCode);

        var rejected = await app.GetAsync("/read", limited);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"));
        Assert.Contains(app.Audit.Entries, e => e.Outcome == ApiKeyAuditOutcome.RateLimited && e.StatusCode == 429);

        // Another key has its own budget; a request without a key is not counted against anyone.
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/read", other)).StatusCode);
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/open", null)).StatusCode);
    }

    [Fact]
    public async Task DefaultLimit_AppliesToKeysWithoutTheirOwn()
    {
        await using var app = await TestApp.StartAsync(o => o.DefaultRequestsPerMinute = 2);
        var key = await app.AddKeyAsync(rateLimit: null);

        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/read", key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/read", key)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await app.GetAsync("/read", key)).StatusCode);
    }

    [Fact]
    public async Task AppOwnedUseRateLimiter_WithAddRateLimiterMiddlewareOff_CountsEachRequestOnce()
    {
        await using var app = await TestApp.StartAsync(o => o.AddRateLimiterMiddleware = false, duplicateRateLimiter: true);
        var key = await app.AddKeyAsync(rateLimit: 3);

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/read", key)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await app.GetAsync("/read", key)).StatusCode);
    }

    [Fact]
    public async Task Disabled_MeansNoLimiting()
    {
        await using var app = await TestApp.StartAsync(o => o.RateLimitingEnabled = false);
        var key = await app.AddKeyAsync(rateLimit: 1);

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/read", key)).StatusCode);
    }
}

public class TenantRuleTests
{
    private static System.Security.Claims.ClaimsPrincipal Principal(params System.Security.Claims.Claim[] claims) =>
        new(new System.Security.Claims.ClaimsIdentity(claims, ApiKeyDefaults.AuthenticationType));

    private static System.Security.Claims.Claim C(string type, string value) => new(type, value);

    [Fact]
    public void UnscopedApiKeyPrincipal_IsDeniedEverywhere()
    {
        // API-key principal with neither tenant_id nor the platform marker: e.g. claims stripped by a buggy enricher.
        var unscoped = Principal(C("auth_method", "api_key"), C("api_key_scope", "admin"));

        Assert.False(unscoped.CanAccessTenant(TestApp.TenantA));
    }

    [Fact]
    public async Task UnscopedApiKeyPrincipal_FailsThePolicy_EvenWhenNoTenantIsAddressed()
    {
        await using var app = await TestApp.StartAsync();
        var authz = app.Services.GetRequiredService<IAuthorizationService>();
        var unscoped = Principal(C("auth_method", "api_key"), C("api_key_scope", "admin"));

        var result = await authz.AuthorizeAsync(unscoped, null, HazinaApiKeyPolicies.Read);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ResourceBasedCheck_UsesTheKeysTenant()
    {
        await using var app = await TestApp.StartAsync();
        var authz = app.Services.GetRequiredService<IAuthorizationService>();
        var tenantKey = Principal(C("auth_method", "api_key"), C("api_key_scope", "write"), C("tenant_id", TestApp.TenantA));

        Assert.True((await authz.AuthorizeAsync(tenantKey, new ApiKeyTenantResource(TestApp.TenantA), HazinaApiKeyPolicies.Write)).Succeeded);
        Assert.False((await authz.AuthorizeAsync(tenantKey, new ApiKeyTenantResource(TestApp.TenantB), HazinaApiKeyPolicies.Write)).Succeeded);
    }

    [Fact]
    public async Task NonApiKeyPrincipal_NeverPassesTheApiKeyPolicies()
    {
        await using var app = await TestApp.StartAsync();
        var authz = app.Services.GetRequiredService<IAuthorizationService>();
        var jwtUser = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            new[] { C("api_key_scope", "admin"), C("tenant_id", TestApp.TenantA) }, "Bearer")); // forged-looking claims, wrong auth method

        Assert.False((await authz.AuthorizeAsync(jwtUser, null, HazinaApiKeyPolicies.Read)).Succeeded);
    }

    [Fact]
    public void BlankTenant_IsNeverAccessible()
    {
        var admin = Principal(C("auth_method", "api_key"), C("api_key_scope", "admin"), C("api_key_platform", "true"));

        Assert.False(admin.CanAccessTenant(""));
        Assert.False(admin.CanAccessTenant(null));
    }
}
