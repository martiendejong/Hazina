using Hazina.DynamicAPI.Core;

namespace Hazina.DynamicAPI.Tests;

public class CredentialStoreTests
{
    private readonly string _testDir;

    public CredentialStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"hazina_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public async Task StoreCredential_ShouldCreateFile()
    {
        // Arrange
        var store = new CredentialStore(_testDir);

        // Act
        await store.StoreCredential("test_service", "api_key", "test_value");

        // Assert
        var filePath = Path.Combine(_testDir, "test_service.json");
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task GetCredential_ShouldReturnStoredValue()
    {
        // Arrange
        var store = new CredentialStore(_testDir);
        await store.StoreCredential("test_service", "api_key", "secret_123");

        // Act
        var value = await store.GetCredential("test_service", "api_key");

        // Assert
        Assert.Equal("secret_123", value);
    }

    [Fact]
    public async Task GetCredential_WhenNotFound_ShouldThrow()
    {
        // Arrange
        var store = new CredentialStore(_testDir);

        // Act & Assert
        await Assert.ThrowsAsync<CredentialNotFoundException>(
            () => store.GetCredential("nonexistent", "api_key")
        );
    }

    [Fact]
    public async Task StoreCredential_WithMultipleKeys_ShouldStoreInSameFile()
    {
        // Arrange
        var store = new CredentialStore(_testDir);

        // Act
        await store.StoreCredential("test_service", "api_key", "key_value");
        await store.StoreCredential("test_service", "secret", "secret_value");

        // Assert
        var apiKey = await store.GetCredential("test_service", "api_key");
        var secret = await store.GetCredential("test_service", "secret");
        Assert.Equal("key_value", apiKey);
        Assert.Equal("secret_value", secret);
    }

    [Fact]
    public void ListServices_ShouldReturnAllServices()
    {
        // Arrange
        var store = new CredentialStore(_testDir);
        File.WriteAllText(Path.Combine(_testDir, "service1.json"), "{}");
        File.WriteAllText(Path.Combine(_testDir, "service2.json"), "{}");

        // Act
        var services = store.ListServices();

        // Assert
        Assert.Contains("service1", services);
        Assert.Contains("service2", services);
        Assert.Equal(2, services.Count());
    }

    [Fact]
    public void ClearCache_ShouldNotThrow()
    {
        // Arrange
        var store = new CredentialStore(_testDir);

        // Act
        var exception = Record.Exception(() => store.ClearCache());

        // Assert - no exception should be thrown
        Assert.Null(exception);
    }

    [Fact]
    public async Task GetCredential_IsCaseInsensitive_ForServiceName()
    {
        // Arrange
        var store = new CredentialStore(_testDir);
        await store.StoreCredential("TestService", "api_key", "test_value");

        // Act
        var value1 = await store.GetCredential("testservice", "api_key");
        var value2 = await store.GetCredential("TESTSERVICE", "api_key");

        // Assert
        Assert.Equal("test_value", value1);
        Assert.Equal("test_value", value2);
    }

}
