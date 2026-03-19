using FluentAssertions;
using Hazina.Store.EmbeddingStore;
using Moq;
using Amazon.S3;
using Amazon.S3.Model;
using System.Net;

namespace Hazina.Store.EmbeddingStore.Tests.CloudStores;

/// <summary>
/// Unit tests for S3VectorStore.
/// Tests S3 storage operations and batch processing.
/// </summary>
public class S3VectorStoreTests
{
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly string _bucketName = "test-bucket";
    private readonly int _dimension = 3;

    public S3VectorStoreTests()
    {
        _mockS3Client = new Mock<IAmazonS3>();

        // Setup bucket location check
        _mockS3Client.Setup(s => s.GetBucketLocationAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetBucketLocationResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Location = S3Region.USEast1
            });
    }

    [Fact]
    public void Constructor_WithEmptyBucketName_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Action act = () => new S3VectorStore(_mockS3Client.Object, "", _dimension);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Bucket name cannot be null or empty*");
    }

    [Fact]
    public void Constructor_WithInvalidDimension_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Action act = () => new S3VectorStore(_mockS3Client.Object, _bucketName, dimension: 0);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Dimension must be positive*");
    }

    [Fact]
    public async Task StoreAsync_ValidEmbedding_StoresSuccessfully()
    {
        // Arrange
        var store = new S3VectorStore(_mockS3Client.Object, _bucketName, _dimension);
        var key = "test-key";
        var checksum = "abc123";
        var embedding = new Embedding(new[] { 1.0, 2.0, 3.0 });

        _mockS3Client.Setup(s => s.PutObjectAsync(
            It.IsAny<PutObjectRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            });

        // Act
        var result = await store.StoreAsync(key, embedding, checksum);

        // Assert
        result.Should().BeTrue();
        _mockS3Client.Verify(s => s.PutObjectAsync(
            It.Is<PutObjectRequest>(req =>
                req.BucketName == _bucketName &&
                req.Key.EndsWith(".json") &&
                req.Metadata.Count > 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StoreAsync_WrongDimension_ThrowsArgumentException()
    {
        // Arrange
        var store = new S3VectorStore(_mockS3Client.Object, _bucketName, _dimension);
        var key = "test-key";
        var checksum = "abc123";
        var embedding = new Embedding(new[] { 1.0, 2.0 }); // Wrong dimension

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await store.StoreAsync(key, embedding, checksum));
    }

    [Fact]
    public async Task GetAsync_ExistingObject_ReturnsEmbeddingInfo()
    {
        // Arrange
        var store = new S3VectorStore(_mockS3Client.Object, _bucketName, _dimension);
        var key = "test-key";
        var checksum = "abc123";
        var embedding = new Embedding(new[] { 1.0, 2.0, 3.0 });
        var embeddingInfo = new EmbeddingInfo(key, checksum, embedding);
        var json = System.Text.Json.JsonSerializer.Serialize(embeddingInfo);

        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        _mockS3Client.Setup(s => s.GetObjectAsync(
            It.IsAny<GetObjectRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                ResponseStream = stream
            });

        // Act
        var result = await store.GetAsync(key);

        // Assert
        result.Should().NotBeNull();
        result!.Key.Should().Be(key);
        result.Checksum.Should().Be(checksum);
        result.Data.Should().HaveCount(_dimension);
    }

    [Fact]
    public async Task GetAsync_NonExistingObject_ReturnsNull()
    {
        // Arrange
        var store = new S3VectorStore(_mockS3Client.Object, _bucketName, _dimension);
        var key = "non-existing-key";

        _mockS3Client.Setup(s => s.GetObjectAsync(
            It.IsAny<GetObjectRequest>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("The specified key does not exist.")
            {
                ErrorCode = "NoSuchKey"
            });

        // Act
        var result = await store.GetAsync(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_ExistingObject_ReturnsTrue()
    {
        // Arrange
        var store = new S3VectorStore(_mockS3Client.Object, _bucketName, _dimension);
        var key = "test-key";

        _mockS3Client.Setup(s => s.DeleteObjectAsync(
            It.IsAny<DeleteObjectRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse
            {
                HttpStatusCode = HttpStatusCode.NoContent
            });

        // Act
        var result = await store.RemoveAsync(key);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ExistingObject_ReturnsTrue()
    {
        // Arrange
        var store = new S3VectorStore(_mockS3Client.Object, _bucketName, _dimension);
        var key = "test-key";

        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(
            It.IsAny<GetObjectMetadataRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            });

        // Act
        var result = await store.ExistsAsync(key);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistingObject_ReturnsFalse()
    {
        // Arrange
        var store = new S3VectorStore(_mockS3Client.Object, _bucketName, _dimension);
        var key = "non-existing-key";

        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(
            It.IsAny<GetObjectMetadataRequest>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not Found")
            {
                ErrorCode = "NotFound"
            });

        // Act
        var result = await store.ExistsAsync(key);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task StoreBatchAsync_ValidBatch_StoresAllEmbeddings()
    {
        // Arrange
        var store = new S3VectorStore(_mockS3Client.Object, _bucketName, _dimension);
        var batch = new[]
        {
            ("key1", new Embedding(new[] { 1.0, 2.0, 3.0 }), "checksum1"),
            ("key2", new Embedding(new[] { 4.0, 5.0, 6.0 }), "checksum2"),
            ("key3", new Embedding(new[] { 7.0, 8.0, 9.0 }), "checksum3")
        };

        _mockS3Client.Setup(s => s.PutObjectAsync(
            It.IsAny<PutObjectRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            });

        // Act
        var result = await store.StoreBatchAsync(batch);

        // Assert
        result.Should().Be(3);
        _mockS3Client.Verify(s => s.PutObjectAsync(
            It.IsAny<PutObjectRequest>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task GetBatchAsync_MultipleKeys_ReturnsAllEmbeddings()
    {
        // Arrange
        var store = new S3VectorStore(_mockS3Client.Object, _bucketName, _dimension);
        var keys = new[] { "key1", "key2", "key3" };

        _mockS3Client.Setup(s => s.GetObjectAsync(
            It.IsAny<GetObjectRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetObjectRequest req, CancellationToken ct) =>
            {
                var embeddingInfo = new EmbeddingInfo("test-key", "checksum", new Embedding(new[] { 1.0, 2.0, 3.0 }));
                var json = System.Text.Json.JsonSerializer.Serialize(embeddingInfo);
                var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

                return new GetObjectResponse
                {
                    HttpStatusCode = HttpStatusCode.OK,
                    ResponseStream = stream
                };
            });

        // Act
        var result = await store.GetBatchAsync(keys);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task SearchSimilarAsync_WithValidQuery_ReturnsTopKResults()
    {
        // Arrange
        var store = new S3VectorStore(_mockS3Client.Object, _bucketName, _dimension);
        var queryEmbedding = new Embedding(new[] { 1.0, 2.0, 3.0 });

        // Setup list objects response
        _mockS3Client.Setup(s => s.ListObjectsV2Async(
            It.IsAny<ListObjectsV2Request>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response
            {
                HttpStatusCode = HttpStatusCode.OK,
                S3Objects = new List<S3Object>
                {
                    new S3Object { Key = "embeddings/key1.json" },
                    new S3Object { Key = "embeddings/key2.json" }
                },
                IsTruncated = false
            });

        // Setup get object responses
        _mockS3Client.Setup(s => s.GetObjectAsync(
            It.IsAny<GetObjectRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetObjectRequest req, CancellationToken ct) =>
            {
                var embeddingInfo = new EmbeddingInfo("test-key", "checksum", new Embedding(new[] { 1.0, 2.0, 3.0 }));
                var json = System.Text.Json.JsonSerializer.Serialize(embeddingInfo);
                var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

                return new GetObjectResponse
                {
                    HttpStatusCode = HttpStatusCode.OK,
                    ResponseStream = stream
                };
            });

        // Act
        var result = await store.SearchSimilarAsync(queryEmbedding, topK: 5);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountLessThanOrEqualTo(5);
    }
}
