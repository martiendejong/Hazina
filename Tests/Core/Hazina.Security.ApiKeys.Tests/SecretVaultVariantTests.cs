using Microsoft.Extensions.DependencyInjection;

namespace Hazina.Security.ApiKeys.Tests;

public class SecretVaultVariantTests
{
    private static ServiceProvider Build(Action<IHazinaApiKeyBuilder> configureVault)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddHazinaApiKeyAuth().UseInMemoryStore();
        configureVault(builder);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task InMemoryVault_KeepsRawKey_AndStoreOnlyGetsTheHash()
    {
        await using var provider = Build(b => b.UseInMemorySecretVault());
        using var scope = provider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IApiKeyManager>();
        var vault = (InMemoryApiKeySecretVault)scope.ServiceProvider.GetRequiredService<IApiKeySecretVault>();

        var issued = await manager.CreateAsync(new ApiKeyCreateRequest { Name = "local", TenantId = TestApp.TenantA, Description = "dev key" });

        var secret = Assert.Single(vault.Secrets).Value;
        Assert.Equal(issued.RawKey, secret.RawKey);

        var stored = (await scope.ServiceProvider.GetRequiredService<IApiKeyStore>().FindByIdAsync(issued.Record.Id))!;
        Assert.Equal(ApiKeyHasher.Hash(issued.RawKey), stored.KeyHash);
        Assert.Equal("dev key", stored.Description);
    }

    [Fact]
    public async Task InMemoryVault_RotationOverwritesTheSameEntry_AndRevokeDeletesIt()
    {
        await using var provider = Build(b => b.UseInMemorySecretVault());
        using var scope = provider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IApiKeyManager>();
        var vault = (InMemoryApiKeySecretVault)scope.ServiceProvider.GetRequiredService<IApiKeySecretVault>();

        var issued = await manager.CreateAsync(new ApiKeyCreateRequest { Name = "local", TenantId = TestApp.TenantA });
        var rotated = await manager.RotateAsync(issued.Record.Id);

        Assert.NotNull(rotated);
        Assert.NotEqual(issued.RawKey, rotated!.RawKey);
        Assert.Equal(rotated.RawKey, Assert.Single(vault.Secrets).Value.RawKey);

        Assert.True(await manager.RevokeAsync(issued.Record.Id));
        Assert.Empty(vault.Secrets);
    }

    [Fact]
    public async Task UnconfiguredVault_FailsClosed_AndLeavesNoKeyBehind()
    {
        await using var provider = Build(b => b.UseUnconfiguredSecretVault());
        using var scope = provider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IApiKeyManager>();
        var store = scope.ServiceProvider.GetRequiredService<InMemoryApiKeyStore>();

        var ex = await Assert.ThrowsAsync<ApiKeyIssuanceException>(() =>
            manager.CreateAsync(new ApiKeyCreateRequest { Id = "nowhere-1", Name = "nowhere", TenantId = TestApp.TenantA }));

        Assert.Contains("No Vault is configured", ex.InnerException?.Message);
        Assert.Null(await store.FindByIdAsync("nowhere-1"));
    }
}
