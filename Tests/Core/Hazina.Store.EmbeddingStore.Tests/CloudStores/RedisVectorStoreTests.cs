using FluentAssertions;
using Hazina.Store.EmbeddingStore;
using Moq;
using StackExchange.Redis;

namespace Hazina.Store.EmbeddingStore.Tests.CloudStores;

/// <summary>
/// Unit tests for RedisVectorStore.
/// Tests both basic Redis operations and batch operations.
/// </summary>
public class RedisVectorStoreTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDb;
    private readonly Mock<IServer> _mockServer;
    private readonly RedisVectorStore _store;
    private readonly int _dimension = 3;

    public RedisVectorStoreTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDb = new Mock<IDatabase>();
        _mockServer = new Mock<IServer>();

        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDb.Object);

        _mockRedis.Setup(r => r.GetEndPoints(It.IsAny<bool>()))
            .Returns(new[] { new DnsEndPoint("localhost", 6379) });

        _mockRedis.Setup(r => r.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
            .Returns(_mockServer.Object);

        _store = new RedisVectorStore(_mockRedis.Object, _dimension, database: 0, useRediSearch: false);
    }

    [Fact]
    public void Constructor_WithInvalidDimension_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Action act = () => new RedisVectorStore(_mockRedis.Object, dimension: 0);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Dimension must be positive*");
    }

    [Fact]
    public async Task StoreAsync_ValidEmbedding_StoresSuccessfully()
    {
        // Arrange
        var key = "test-key";
        var checksum = "abc123";
        var embedding = new Embedding(new[] { 1.0, 2.0, 3.0 });

        _mockDb.Setup(db => db.HashSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<HashEntry[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _store.StoreAsync(key, embedding, checksum);

        // Assert
        result.Should().BeTrue();
        _mockDb.Verify(db => db.HashSetAsync(
            It.Is<RedisKey>(k => k.ToString().Contains(key)),
            It.Is<HashEntry[]>(entries => entries.Length == 5),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task StoreAsync_WrongDimension_ThrowsArgumentException()
    {
        // Arrange
        var key = "test-key";
        var checksum = "abc123";
        var embedding = new Embedding(new[] { 1.0, 2.0 }); // Wrong dimension

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _store.StoreAsync(key, embedding, checksum));
    }

    [Fact]
    public async Task GetAsync_ExistingKey_ReturnsEmbeddingInfo()
    {
        // Arrange
        var key = "test-key";
        var checksum = "abc123";
        var embeddingArray = new[] { 1.0, 2.0, 3.0 };
        var embeddingJson = System.Text.Json.JsonSerializer.Serialize(embeddingArray);

        var hashEntries = new HashEntry[]
        {
            new HashEntry("key", key),
            new HashEntry("checksum", checksum),
            new HashEntry("embedding", embeddingJson),
            new HashEntry("dimension", _dimension)
        };

        _mockDb.Setup(db => db.HashGetAllAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(hashEntries);

        // Act
        var result = await _store.GetAsync(key);

        // Assert
        result.Should().NotBeNull();
        result!.Key.Should().Be(key);
        result.Checksum.Should().Be(checksum);
        result.Data.Should().HaveCount(_dimension);
    }

    [Fact]
    public async Task GetAsync_NonExistingKey_ReturnsNull()
    {
        // Arrange
        var key = "non-existing-key";

        _mockDb.Setup(db => db.HashGetAllAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<HashEntry>());

        // Act
        var result = await _store.GetAsync(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_ExistingKey_ReturnsTrue()
    {
        // Arrange
        var key = "test-key";

        _mockDb.Setup(db => db.KeyDeleteAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _store.RemoveAsync(key);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ExistingKey_ReturnsTrue()
    {
        // Arrange
        var key = "test-key";

        _mockDb.Setup(db => db.KeyExistsAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _store.ExistsAsync(key);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task StoreBatchAsync_ValidBatch_StoresAllEmbeddings()
    {
        // Arrange
        var batch = new[]
        {
            ("key1", new Embedding(new[] { 1.0, 2.0, 3.0 }), "checksum1"),
            ("key2", new Embedding(new[] { 4.0, 5.0, 6.0 }), "checksum2"),
            ("key3", new Embedding(new[] { 7.0, 8.0, 9.0 }), "checksum3")
        };

        var mockTransaction = new Mock<ITransaction>();
        mockTransaction.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        mockTransaction.Setup(t => t.HashSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<HashEntry[]>(),
            It.IsAny<CommandFlags>()))
            .Returns(Task.FromResult(true).ContinueWith(_ => true));

        _mockDb.Setup(db => db.CreateTransaction(It.IsAny<object>()))
            .Returns(mockTransaction.Object);

        // Act
        var result = await _store.StoreBatchAsync(batch);

        // Assert
        result.Should().Be(3);
        mockTransaction.Verify(t => t.HashSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<HashEntry[]>(),
            It.IsAny<CommandFlags>()), Times.Exactly(3));
    }

    [Fact]
    public async Task GetBatchAsync_MultipleKeys_ReturnsAllEmbeddings()
    {
        // Arrange
        var keys = new[] { "key1", "key2", "key3" };

        _mockDb.Setup(db => db.HashGetAllAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags flags) =>
            {
                var keyStr = key.ToString().Split(':').Last();
                var embeddingArray = new[] { 1.0, 2.0, 3.0 };
                var embeddingJson = System.Text.Json.JsonSerializer.Serialize(embeddingArray);

                return new HashEntry[]
                {
                    new HashEntry("key", keyStr),
                    new HashEntry("checksum", "checksum"),
                    new HashEntry("embedding", embeddingJson),
                    new HashEntry("dimension", _dimension)
                };
            });

        // Act
        var result = await _store.GetBatchAsync(keys);

        // Assert
        result.Should().HaveCount(3);
        result.Select(e => e.Key).Should().Contain(keys);
    }

    public void Dispose()
    {
        _store?.Dispose();
    }
}
