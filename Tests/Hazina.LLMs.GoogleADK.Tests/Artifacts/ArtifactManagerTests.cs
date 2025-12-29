using Hazina.LLMs.GoogleADK.Artifacts;
using Hazina.LLMs.GoogleADK.Artifacts.Models;
using Hazina.LLMs.GoogleADK.Artifacts.Storage;
using Xunit;

namespace Hazina.LLMs.GoogleADK.Tests.Artifacts;

public class ArtifactManagerTests : IDisposable
{
    private readonly string _testPath;
    private readonly ArtifactManager _manager;

    public ArtifactManagerTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), "artifact-tests-" + Guid.NewGuid());
        var storage = new FileSystemArtifactStorage(_testPath);
        _manager = new ArtifactManager(storage);
    }

    [Fact]
    public async Task CreateFromTextAsync_ShouldCreateArtifact()
    {
        // Arrange
        var text = "Hello, World!";

        // Act
        var artifact = await _manager.CreateFromTextAsync(text, "test.txt", "agent-1");

        // Assert
        Assert.NotNull(artifact);
        Assert.Equal("test.txt", artifact.Name);
        Assert.Equal(ArtifactType.Text, artifact.Type);
        Assert.Equal("text/plain", artifact.MimeType);
        Assert.Equal("agent-1", artifact.AgentId);
        Assert.True(artifact.Size > 0);
    }

    [Fact]
    public async Task CreateFromDataAsync_ShouldStoreData()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var artifact = await _manager.CreateFromDataAsync(data, "test.bin", "application/octet-stream", "agent-1");

        // Assert
        Assert.NotNull(artifact);
        Assert.Equal(5, artifact.Size);
    }

    [Fact]
    public async Task GetArtifactAsync_ShouldRetrieveArtifact()
    {
        // Arrange
        var text = "Test content";
        var created = await _manager.CreateFromTextAsync(text, "test.txt");

        // Act
        var retrieved = await _manager.GetArtifactAsync(created.ArtifactId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(created.ArtifactId, retrieved.ArtifactId);
        Assert.Equal(created.Name, retrieved.Name);
    }

    [Fact]
    public async Task GetArtifactTextAsync_ShouldReturnText()
    {
        // Arrange
        var text = "Test content";
        var artifact = await _manager.CreateFromTextAsync(text, "test.txt");

        // Act
        var retrieved = await _manager.GetArtifactTextAsync(artifact.ArtifactId);

        // Assert
        Assert.Equal(text, retrieved);
    }

    [Fact]
    public async Task DeleteArtifactAsync_ShouldRemoveArtifact()
    {
        // Arrange
        var artifact = await _manager.CreateFromTextAsync("test", "test.txt");

        // Act
        var deleted = await _manager.DeleteArtifactAsync(artifact.ArtifactId);
        var retrieved = await _manager.GetArtifactAsync(artifact.ArtifactId);

        // Assert
        Assert.True(deleted);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task SearchAsync_ShouldFindArtifacts()
    {
        // Arrange
        await _manager.CreateFromTextAsync("test1", "test1.txt", "agent-1");
        await _manager.CreateFromTextAsync("test2", "test2.txt", "agent-1");
        await _manager.CreateFromTextAsync("test3", "test3.txt", "agent-2");

        // Act
        var results = await _manager.SearchAsync(new ArtifactQuery
        {
            AgentId = "agent-1"
        });

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, a => Assert.Equal("agent-1", a.AgentId));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testPath))
        {
            Directory.Delete(_testPath, true);
        }
    }
}
