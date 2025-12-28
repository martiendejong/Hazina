using Hazina.LLMs.GoogleADK.Memory;
using Hazina.LLMs.GoogleADK.Memory.Models;
using Hazina.LLMs.GoogleADK.Memory.Storage;
using Xunit;

namespace Hazina.LLMs.GoogleADK.Tests.Memory;

public class MemoryBankTests
{
    [Fact]
    public async Task StoreMemoryAsync_ShouldCreateMemory()
    {
        // Arrange
        var storage = new InMemoryMemoryStorage();
        var memoryBank = new MemoryBank(storage);

        // Act
        var memory = await memoryBank.StoreMemoryAsync(
            content: "The sky is blue",
            type: MemoryType.Semantic,
            agentName: "TestAgent",
            importance: 0.8
        );

        // Assert
        Assert.NotNull(memory);
        Assert.Equal("The sky is blue", memory.Content);
        Assert.Equal(MemoryType.Semantic, memory.Type);
        Assert.Equal(0.8, memory.Importance);

        await memoryBank.DisposeAsync();
    }

    [Fact]
    public async Task SearchByTextAsync_ShouldFindMatchingMemories()
    {
        // Arrange
        var storage = new InMemoryMemoryStorage();
        var memoryBank = new MemoryBank(storage);

        await memoryBank.StoreMemoryAsync("The sky is blue", MemoryType.Semantic);
        await memoryBank.StoreMemoryAsync("The grass is green", MemoryType.Semantic);
        await memoryBank.StoreMemoryAsync("Blue is a color", MemoryType.Semantic);

        // Act
        var results = await memoryBank.SearchByTextAsync("blue", limit: 10);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("blue", r.Memory.Content, StringComparison.OrdinalIgnoreCase));

        await memoryBank.DisposeAsync();
    }

    [Fact]
    public async Task GetRecentMemoriesAsync_ShouldReturnNewestFirst()
    {
        // Arrange
        var storage = new InMemoryMemoryStorage();
        var memoryBank = new MemoryBank(storage);

        var memory1 = await memoryBank.StoreMemoryAsync("First", MemoryType.Episodic);
        await Task.Delay(10);
        var memory2 = await memoryBank.StoreMemoryAsync("Second", MemoryType.Episodic);
        await Task.Delay(10);
        var memory3 = await memoryBank.StoreMemoryAsync("Third", MemoryType.Episodic);

        // Act
        var recent = await memoryBank.GetRecentMemoriesAsync(limit: 2);

        // Assert
        Assert.Equal(2, recent.Count);
        Assert.Equal("Third", recent[0].Content);
        Assert.Equal("Second", recent[1].Content);

        await memoryBank.DisposeAsync();
    }

    [Fact]
    public async Task GetImportantMemoriesAsync_ShouldFilterByImportance()
    {
        // Arrange
        var storage = new InMemoryMemoryStorage();
        var memoryBank = new MemoryBank(storage);

        await memoryBank.StoreMemoryAsync("Low importance", MemoryType.Semantic, importance: 0.3);
        await memoryBank.StoreMemoryAsync("High importance", MemoryType.Semantic, importance: 0.9);
        await memoryBank.StoreMemoryAsync("Medium importance", MemoryType.Semantic, importance: 0.5);

        // Act
        var important = await memoryBank.GetImportantMemoriesAsync(minImportance: 0.7);

        // Assert
        Assert.Single(important);
        Assert.Equal("High importance", important[0].Content);

        await memoryBank.DisposeAsync();
    }

    [Fact]
    public async Task ConsolidateMemoriesAsync_ShouldRemoveWeakMemories()
    {
        // Arrange
        var storage = new InMemoryMemoryStorage();
        var memoryBank = new MemoryBank(storage);

        // Create memories with different access patterns
        var weakMemory = await memoryBank.StoreMemoryAsync("Weak", MemoryType.Working, importance: 0.1);
        var strongMemory = await memoryBank.StoreMemoryAsync("Strong", MemoryType.Semantic, importance: 0.9);

        // Make strong memory stronger by accessing it
        await memoryBank.RetrieveMemoryAsync(strongMemory.MemoryId);
        await memoryBank.RetrieveMemoryAsync(strongMemory.MemoryId);

        // Act
        var removed = await memoryBank.ConsolidateMemoriesAsync(strengthThreshold: 0.2);

        // Assert
        Assert.True(removed > 0);

        await memoryBank.DisposeAsync();
    }

    [Fact]
    public async Task UpdateImportanceAsync_ShouldModifyImportance()
    {
        // Arrange
        var storage = new InMemoryMemoryStorage();
        var memoryBank = new MemoryBank(storage);

        var memory = await memoryBank.StoreMemoryAsync("Test", MemoryType.Semantic, importance: 0.5);

        // Act
        await memoryBank.UpdateImportanceAsync(memory.MemoryId, 0.9);
        var retrieved = await memoryBank.RetrieveMemoryAsync(memory.MemoryId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(0.9, retrieved.Importance);

        await memoryBank.DisposeAsync();
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnCorrectStats()
    {
        // Arrange
        var storage = new InMemoryMemoryStorage();
        var memoryBank = new MemoryBank(storage);

        await memoryBank.StoreMemoryAsync("Episodic 1", MemoryType.Episodic);
        await memoryBank.StoreMemoryAsync("Episodic 2", MemoryType.Episodic);
        await memoryBank.StoreMemoryAsync("Semantic 1", MemoryType.Semantic);

        // Act
        var stats = await memoryBank.GetStatisticsAsync();

        // Assert
        Assert.Equal(3, stats.TotalMemories);
        Assert.Equal(2, stats.MemoriesByType[MemoryType.Episodic]);
        Assert.Equal(1, stats.MemoriesByType[MemoryType.Semantic]);

        await memoryBank.DisposeAsync();
    }
}
