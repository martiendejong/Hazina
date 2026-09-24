using System.Net;
using System.Text.Json;

namespace Hazina.Security.ApiKeys.Tests;

public class MiddlewareTests
{
    [Fact]
    public async Task ValidKey_OwnTenant_Returns200()
    {
        await using var app = await TestApp.StartAsync();
        var key = await app.AddKeyAsync(ApiKeyScope.Read, TestApp.TenantA);

        var response = await app.GetAsync($"/tenants/{TestApp.TenantA}/data", key);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ValidKey_OtherTenantInRoute_Returns403()
    {
        await using var app = await TestApp.StartAsync();
        var key = await app.AddKeyAsync(ApiKeyScope.Admin, TestApp.TenantA); // even admin scope stays inside its tenant

        var response = await app.GetAsync($"/tenants/{TestApp.TenantB}/data", key);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(app.Audit.Entries, e => e.Outcome == ApiKeyAuditOutcome.TenantMismatch && e.TenantId == TestApp.TenantA);
    }

    [Theory]
    [InlineData("/read?tenantId=22222222-2222-2222-2222-222222222222")]
    [InlineData("/open?tenantId=22222222-2222-2222-2222-222222222222")] // even endpoints without a policy are guarded by the middleware
    public async Task ValidKey_OtherTenantInQuery_Returns403BeforeRouting(string path)
    {
        await using var app = await TestApp.StartAsync();
        var key = await app.AddKeyAsync(ApiKeyScope.Admin, TestApp.TenantA);

        var response = await app.GetAsync(path, key);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ValidKey_OtherTenantInHeader_Returns403()
    {
        await using var app = await TestApp.StartAsync();
        var key = await app.AddKeyAsync(ApiKeyScope.Read, TestApp.TenantA);

        var response = await app.SendAsync(HttpMethod.Get, "/read", key, r => r.Headers.Add("X-Tenant-Id", TestApp.TenantB));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TenantId_GuidCaseAndFormat_DoesNotCauseFalseMismatch()
    {
        await using var app = await TestApp.StartAsync();
        var key = await app.AddKeyAsync(ApiKeyScope.Read, "AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");

        var response = await app.GetAsync("/tenants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/data", key);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PlatformKey_WithoutAdminScope_CannotAddressATenant()
    {
        await using var app = await TestApp.StartAsync();
        var key = await app.AddKeyAsync(ApiKeyScope.Write, tenantId: null); // no tenant = platform key

        Assert.Equal(HttpStatusCode.Forbidden, (await app.GetAsync($"/tenants/{TestApp.TenantA}/data", key)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.GetAsync($"/read?tenantId={TestApp.TenantA}", key)).StatusCode);
        // ...but still works for endpoints that address no tenant.
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/read", key)).StatusCode);
    }

    [Fact]
    public async Task PlatformKey_WithAdminScope_MayAddressAnyTenant()
    {
        await using var app = await TestApp.StartAsync();
        var key = await app.AddKeyAsync(ApiKeyScope.Admin, tenantId: null);

        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync($"/tenants/{TestApp.TenantA}/data", key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync($"/tenants/{TestApp.TenantB}/data", key)).StatusCode);
    }

    [Fact]
    public async Task Scope_Read_CannotWriteOrAdmin()
    {
        await using var app = await TestApp.StartAsync();
        var key = await app.AddKeyAsync(ApiKeyScope.Read);

        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/read", key)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.SendAsync(HttpMethod.Post, "/write", key)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.GetAsync("/admin", key)).StatusCode);
    }

    [Fact]
    public async Task Scope_Write_CanReadAndWrite_ButNotAdmin()
    {
        await using var app = await TestApp.StartAsync();
        var key = await app.AddKeyAsync(ApiKeyScope.Write);

        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/read", key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.SendAsync(HttpMethod.Post, "/write", key)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.GetAsync("/admin", key)).StatusCode);
    }

    [Fact]
    public async Task Scope_Admin_CanDoEverything()
    {
        await using var app = await TestApp.StartAsync();
        var key = await app.AddKeyAsync(ApiKeyScope.Admin);

        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/read", key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.SendAsync(HttpMethod.Post, "/write", key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/admin", key)).StatusCode);
    }

    [Fact]
    public async Task ExpiredKey_Returns401()
    {
        await using var app = await TestApp.StartAsync();
        var key = await app.AddKeyAsync(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/read", key)).StatusCode);
        Assert.Contains(app.Audit.Entries, e => e.Outcome == ApiKeyAuditOutcome.Expired);
    }

    [Fact]
    public async Task RevokedKey_Returns401()
    {
        await using var app = await TestApp.StartAsync();
        var key = await app.AddKeyAsync(active: false);

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/read", key)).StatusCode);
        Assert.Contains(app.Audit.Entries, e => e.Outcome == ApiKeyAuditOutcome.Revoked);
    }

    [Fact]
    public async Task UnknownKey_Returns401_WithGenericMessage()
    {
        await using var app = await TestApp.StartAsync();

        var response = await app.GetAsync("/read", "not-a-real-key");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Invalid or expired API key", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EmptyKeyHeader_Returns401()
    {
        await using var app = await TestApp.StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/open", "   ")).StatusCode);
    }

    [Fact]
    public async Task DuplicateKeyHeaders_AreRejectedAsAmbiguous()
    {
        await using var app = await TestApp.StartAsync();
        var key = await app.AddKeyAsync();

        var response = await app.SendAsync(HttpMethod.Get, "/read", key, r => r.Headers.TryAddWithoutValidation("X-Api-Key", key));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NoHeader_PassesThrough_AndProtectedEndpointChallenges()
    {
        await using var app = await TestApp.StartAsync();

        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/open", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/read", null)).StatusCode);
        Assert.Empty(app.Audit.Entries); // requests without the header are not API-key requests
    }

    [Fact]
    public async Task AllowedIps_FailClosedWhenCallerAddressUnknown()
    {
        await using var app = await TestApp.StartAsync();
        var key = await app.AddKeyAsync(allowedIps: new[] { "10.0.0.5" }); // TestServer has no remote address

        var response = await app.GetAsync("/read", key);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(app.Audit.Entries, e => e.Outcome == ApiKeyAuditOutcome.IpNotAllowed);
    }

    [Fact]
    public async Task ExcludedPathPrefix_SkipsKeyAuthentication()
    {
        await using var app = await TestApp.StartAsync(o => o.ExcludedPathPrefixes.Add("/oauth/"));

        // A garbage key on an excluded path must not 401: those endpoints authenticate their own way.
        var response = await app.GetAsync("/oauth/token", "garbage");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Principal_HasIamShapedClaims_AndExtraClaimsCannotOverrideSecurityClaims()
    {
        await using var app = await TestApp.StartAsync(useAuthentication: true);
        var key = await app.AddKeyAsync(
            ApiKeyScope.Write, TestApp.TenantA, userId: "user-42", name: "ci",
            permissions: new[] { "users:read", "roles:read" },
            extraClaims: new[]
            {
                new System.Security.Claims.Claim("email", "ci@example.com"),
                new System.Security.Claims.Claim("tenant_id", TestApp.TenantB), // must be ignored
                new System.Security.Claims.Claim("api_key_scope", "admin"),      // must be ignored
            });

        var response = await app.GetAsync("/whoami", key);
        var claims = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(await response.Content.ReadAsStringAsync())!
            .Select(c => (Type: c["type"], Value: c["value"])).ToList();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(("auth_method", "api_key"), claims);
        Assert.Contains(("api_key_scope", "write"), claims);
        Assert.Contains(("tenant_id", TestApp.TenantA), claims);
        Assert.Contains(("sub", "user-42"), claims);
        Assert.Contains(("permission", "users:read"), claims);
        Assert.Contains(("permission", "roles:read"), claims);
        Assert.Contains(("email", "ci@example.com"), claims);
        Assert.Single(claims, c => c.Type == "tenant_id");
        Assert.Single(claims, c => c.Type == "api_key_scope");
        Assert.Contains(claims, c => c.Type == "api_key_prefix" && c.Value.StartsWith("tst_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CustomHeaderName_IsHonoured()
    {
        await using var app = await TestApp.StartAsync(o => o.HeaderName = "X-Service-Key");
        var key = await app.AddKeyAsync();

        var ok = await app.SendAsync(HttpMethod.Get, "/read", null, r => r.Headers.Add("X-Service-Key", key));
        var ignored = await app.GetAsync("/open", "garbage"); // default header is no longer looked at

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ignored.StatusCode);
    }
}
