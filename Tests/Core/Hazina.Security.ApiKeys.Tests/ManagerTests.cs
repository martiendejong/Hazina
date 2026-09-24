using System.Net;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.Security.ApiKeys.Tests;

public class ManagerTests
{
    private static async Task<(TestApp App, IServiceScope Scope, IApiKeyManager Manager)> StartAsync()
    {
        var app = await TestApp.StartAsync();
        var scope = app.Services.CreateScope();
        return (app, scope, scope.ServiceProvider.GetRequiredService<IApiKeyManager>());
    }

    [Fact]
    public async Task Create_StoresRawKeyInVault_AndOnlyTheHashInTheStore()
    {
        var (app, scope, manager) = await StartAsync();
        using var _ = scope; await using var __ = app;

        var issued = await manager.CreateAsync(new ApiKeyCreateRequest { Name = "ci", Scope = ApiKeyScope.Write, TenantId = TestApp.TenantA, UserId = "u1" });

        // Vault holds the raw key...
        var secret = Assert.Single(app.Vault.Secrets).Value;
        Assert.Equal(issued.RawKey, secret.RawKey);
        Assert.Equal(issued.Record.VaultReference, app.Vault.Secrets.Keys.Single());

        // ...the app store holds the hash and nothing derivable back to the raw key.
        var stored = (await app.Store.FindByIdAsync(issued.Record.Id))!;
        Assert.Equal(ApiKeyHasher.Hash(issued.RawKey), stored.KeyHash);
        Assert.DoesNotContain(issued.RawKey, PublicStringValues(stored));
        Assert.StartsWith("hzn_", issued.RawKey);
        Assert.Equal(stored.KeyPrefix, issued.RawKey[..stored.KeyPrefix.Length]);
    }

    [Fact]
    public async Task CreatedKey_AuthenticatesEndToEnd_WithItsScopeAndTenant()
    {
        var (app, scope, manager) = await StartAsync();
        using var _ = scope; await using var __ = app;
        var issued = await manager.CreateAsync(new ApiKeyCreateRequest { Name = "ci", Scope = ApiKeyScope.Write, TenantId = TestApp.TenantA });

        Assert.Equal(HttpStatusCode.OK, (await app.SendAsync(HttpMethod.Post, "/write", issued.RawKey)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.GetAsync("/admin", issued.RawKey)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.GetAsync($"/tenants/{TestApp.TenantB}/data", issued.RawKey)).StatusCode);
    }

    [Fact]
    public async Task Create_WhenVaultFails_IssuesNoKeyAtAll()
    {
        var (app, scope, manager) = await StartAsync();
        using var _ = scope; await using var __ = app;
        app.Vault.FailStore = true;

        await Assert.ThrowsAsync<ApiKeyIssuanceException>(() =>
            manager.CreateAsync(new ApiKeyCreateRequest { Name = "ci", TenantId = TestApp.TenantA }));

        Assert.Null(await app.Store.FindByHashAsync("anything"));
        Assert.Empty(app.Vault.Secrets);
    }

    [Fact]
    public async Task Rotate_InvalidatesTheOldKeyImmediately_AndUpdatesVault()
    {
        var (app, scope, manager) = await StartAsync();
        using var _ = scope; await using var __ = app;
        var issued = await manager.CreateAsync(new ApiKeyCreateRequest { Name = "ci", TenantId = TestApp.TenantA });
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/read", issued.RawKey)).StatusCode); // warms the cache

        var rotated = await manager.RotateAsync(issued.Record.Id);

        Assert.NotNull(rotated);
        Assert.NotEqual(issued.RawKey, rotated!.RawKey);
        Assert.Equal(issued.Record.KeyPrefix, rotated.Record.KeyPrefix); // prefix continuity
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/read", issued.RawKey)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/read", rotated.RawKey)).StatusCode);
        Assert.Equal(rotated.RawKey, Assert.Single(app.Vault.Secrets).Value.RawKey); // same credential, new raw key
    }

    [Fact]
    public async Task Rotate_WhenVaultFails_KeepsTheOldKeyValid()
    {
        var (app, scope, manager) = await StartAsync();
        using var _ = scope; await using var __ = app;
        var issued = await manager.CreateAsync(new ApiKeyCreateRequest { Name = "ci", TenantId = TestApp.TenantA });
        app.Vault.FailStore = true;

        await Assert.ThrowsAsync<ApiKeyIssuanceException>(() => manager.RotateAsync(issued.Record.Id));

        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/read", issued.RawKey)).StatusCode);
        Assert.Equal(issued.RawKey, Assert.Single(app.Vault.Secrets).Value.RawKey); // vault still matches the valid key
    }

    [Fact]
    public async Task Rotate_UnknownKey_ReturnsNull()
    {
        var (app, scope, manager) = await StartAsync();
        using var _ = scope; await using var __ = app;

        Assert.Null(await manager.RotateAsync("nope"));
    }

    [Fact]
    public async Task Revoke_RejectsTheKeyImmediately_AndRemovesItFromVault()
    {
        var (app, scope, manager) = await StartAsync();
        using var _ = scope; await using var __ = app;
        var issued = await manager.CreateAsync(new ApiKeyCreateRequest { Name = "ci", TenantId = TestApp.TenantA });
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/read", issued.RawKey)).StatusCode);

        Assert.True(await manager.RevokeAsync(issued.Record.Id));

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/read", issued.RawKey)).StatusCode);
        Assert.Empty(app.Vault.Secrets);
        Assert.False(await manager.RevokeAsync("nope"));
    }

    /// <summary>Every public string-ish value of the record, so a raw key hiding in any property would be caught.</summary>
    private static string PublicStringValues(ApiKeyRecord record) =>
        string.Join('|', typeof(ApiKeyRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.GetValue(record))
            .SelectMany(v => v switch
            {
                string s => new[] { s },
                IEnumerable<string> list => list,
                null => Array.Empty<string>(),
                _ => new[] { v.ToString() ?? string.Empty },
            }));
}
