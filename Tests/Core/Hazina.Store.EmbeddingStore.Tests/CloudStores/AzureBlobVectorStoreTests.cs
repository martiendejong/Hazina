using FluentAssertions;
using Hazina.Store.EmbeddingStore;
using Moq;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure;
using System.Text;

namespace Hazina.Store.EmbeddingStore.Tests.CloudStores;

/// <summary>
/// Unit tests for AzureBlobVectorStore.
/// Tests blob storage operations and batch processing.
/// </summary>
public class AzureBlobVectorStoreTests
{
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;
    private readonly Mock<BlobContainerClient> _mockContainerClient;
    private readonly Mock<BlobClient> _mockBlobClient;
    private readonly int _dimension = 3;

    public AzureBlobVectorStoreTests()
    {
        _mockBlobServiceClient = new Mock<BlobServiceClient>();
        _mockContainerClient = new Mock<BlobContainerClient>();
        _mockBlobClient = new Mock<BlobClient>();

        _mockBlobServiceClient.Setup(s => s.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(_mockContainerClient.Object);

        _mockContainerClient.Setup(c => c.CreateIfNotExists(
            It.IsAny<PublicAccessType>(),
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<CancellationToken>()))
            .Returns((Response<BlobContainerInfo>?)null);

        _mockContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(_mockBlobClient.Object);
    }

    [Fact]
    public void Constructor_WithEmptyContainerName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Action act = () => new AzureBlobVectorStore(_mockBlobServiceClient.Object, "", _dimension);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Container name cannot be null or empty*");
    }

    [Fact]
    public void Constructor_WithInvalidDimension_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Action act = () => new AzureBlobVectorStore(_mockBlobServiceClient.Object, "test-container", dimension: 0);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Dimension must be positive*");
    }

    [Fact]
    public async Task StoreAsync_ValidEmbedding_StoresSuccessfully()
    {
        // Arrange
        var store = new AzureBlobVectorStore(_mockBlobServiceClient.Object, "test-container", _dimension);
        var key = "test-key";
        var checksum = "abc123";
        var embedding = new Embedding(new[] { 1.0, 2.0, 3.0 });

        var mockResponse = new Mock<Response<BlobContentInfo>>();
        _mockBlobClient.Setup(b => b.UploadAsync(
            It.IsAny<Stream>(),
            It.IsAny<BlobUploadOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Act
        var result = await store.StoreAsync(key, embedding, checksum);

        // Assert
        result.Should().BeTrue();
        _mockBlobClient.Verify(b => b.UploadAsync(
            It.IsAny<Stream>(),
            It.Is<BlobUploadOptions>(opts =>
                opts.Metadata != null &&
                opts.Metadata.ContainsKey("key") &&
                opts.Metadata.ContainsKey("checksum")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StoreAsync_WrongDimension_ThrowsArgumentException()
    {
        // Arrange
        var store = new AzureBlobVectorStore(_mockBlobServiceClient.Object, "test-container", _dimension);
        var key = "test-key";
        var checksum = "abc123";
        var embedding = new Embedding(new[] { 1.0, 2.0 }); // Wrong dimension

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await store.StoreAsync(key, embedding, checksum));
    }

    [Fact]
    public async Task GetAsync_ExistingBlob_ReturnsEmbeddingInfo()
    {
        // Arrange
        var store = new AzureBlobVectorStore(_mockBlobServiceClient.Object, "test-container", _dimension);
        var key = "test-key";
        var checksum = "abc123";
        var embedding = new Embedding(new[] { 1.0, 2.0, 3.0 });
        var embeddingInfo = new EmbeddingInfo(key, checksum, embedding);
        var json = System.Text.Json.JsonSerializer.Serialize(embeddingInfo);

        var mockExistsResponse = new Mock<Response<bool>>();
        mockExistsResponse.Setup(r => r.Value).Returns(true);

        _mockBlobClient.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockExistsResponse.Object);

        var binaryData = BinaryData.FromString(json);
        var mockDownloadResponse = new Mock<Response<BlobDownloadResult>>();
        mockDownloadResponse.Setup(r => r.Value.Content).Returns(binaryData);

        _mockBlobClient.Setup(b => b.DownloadContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDownloadResponse.Object);

        // Act
        var result = await store.GetAsync(key);

        // Assert
        result.Should().NotBeNull();
        result!.Key.Should().Be(key);
        result.Checksum.Should().Be(checksum);
        result.Data.Should().HaveCount(_dimension);
    }

    [Fact]
    public async Task GetAsync_NonExistingBlob_ReturnsNull()
    {
        // Arrange
        var store = new AzureBlobVectorStore(_mockBlobServiceClient.Object, "test-container", _dimension);
        var key = "non-existing-key";

        var mockExistsResponse = new Mock<Response<bool>>();
        mockExistsResponse.Setup(r => r.Value).Returns(false);

        _mockBlobClient.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockExistsResponse.Object);

        // Act
        var result = await store.GetAsync(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_ExistingBlob_ReturnsTrue()
    {
        // Arrange
        var store = new AzureBlobVectorStore(_mockBlobServiceClient.Object, "test-container", _dimension);
        var key = "test-key";

        var mockDeleteResponse = new Mock<Response<bool>>();
        mockDeleteResponse.Setup(r => r.Value).Returns(true);

        _mockBlobClient.Setup(b => b.DeleteIfExistsAsync(
            It.IsAny<DeleteSnapshotsOption>(),
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeleteResponse.Object);

        // Act
        var result = await store.RemoveAsync(key);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ExistingBlob_ReturnsTrue()
    {
        // Arrange
        var store = new AzureBlobVectorStore(_mockBlobServiceClient.Object, "test-container", _dimension);
        var key = "test-key";

        var mockExistsResponse = new Mock<Response<bool>>();
        mockExistsResponse.Setup(r => r.Value).Returns(true);

        _mockBlobClient.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockExistsResponse.Object);

        // Act
        var result = await store.ExistsAsync(key);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task StoreBatchAsync_ValidBatch_StoresAllEmbeddings()
    {
        // Arrange
        var store = new AzureBlobVectorStore(_mockBlobServiceClient.Object, "test-container", _dimension);
        var batch = new[]
        {
            ("key1", new Embedding(new[] { 1.0, 2.0, 3.0 }), "checksum1"),
            ("key2", new Embedding(new[] { 4.0, 5.0, 6.0 }), "checksum2"),
            ("key3", new Embedding(new[] { 7.0, 8.0, 9.0 }), "checksum3")
        };

        var mockResponse = new Mock<Response<BlobContentInfo>>();
        _mockBlobClient.Setup(b => b.UploadAsync(
            It.IsAny<Stream>(),
            It.IsAny<BlobUploadOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Act
        var result = await store.StoreBatchAsync(batch);

        // Assert
        result.Should().Be(3);
        _mockBlobClient.Verify(b => b.UploadAsync(
            It.IsAny<Stream>(),
            It.IsAny<BlobUploadOptions>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task GetBatchAsync_MultipleKeys_ReturnsAllEmbeddings()
    {
        // Arrange
        var store = new AzureBlobVectorStore(_mockBlobServiceClient.Object, "test-container", _dimension);
        var keys = new[] { "key1", "key2", "key3" };

        _mockBlobClient.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancellationToken ct) =>
            {
                var mockExistsResponse = new Mock<Response<bool>>();
                mockExistsResponse.Setup(r => r.Value).Returns(true);
                return mockExistsResponse.Object;
            });

        _mockBlobClient.Setup(b => b.DownloadContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancellationToken ct) =>
            {
                var embeddingInfo = new EmbeddingInfo("test-key", "checksum", new Embedding(new[] { 1.0, 2.0, 3.0 }));
                var json = System.Text.Json.JsonSerializer.Serialize(embeddingInfo);
                var binaryData = BinaryData.FromString(json);

                var mockDownloadResponse = new Mock<Response<BlobDownloadResult>>();
                mockDownloadResponse.Setup(r => r.Value.Content).Returns(binaryData);
                return mockDownloadResponse.Object;
            });

        // Act
        var result = await store.GetBatchAsync(keys);

        // Assert
        result.Should().HaveCount(3);
    }
}
