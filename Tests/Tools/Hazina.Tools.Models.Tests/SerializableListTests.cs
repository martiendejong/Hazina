using Hazina.Tools.Models;
using FluentAssertions;

namespace Hazina.Tools.Models.Tests;

public class SerializableListTests : IDisposable
{
    private readonly string _testFilePath;

    public SerializableListTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_list_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public void Constructor_WithNoArguments_ShouldCreateEmptyList()
    {
        // Act
        var list = new SerializableList<string>();

        // Assert
        list.Should().BeEmpty();
        list.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithItems_ShouldInitializeWithItems()
    {
        // Arrange
        var items = new[] { "item1", "item2", "item3" };

        // Act
        var list = new SerializableList<string>(items);

        // Assert
        list.Should().HaveCount(3);
        list.Should().ContainInOrder(items);
    }

    [Fact]
    public void Serialize_WithStringList_ShouldReturnValidJson()
    {
        // Arrange
        var list = new SerializableList<string> { "apple", "banana", "cherry" };

        // Act
        var json = list.Serialize();

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("apple");
        json.Should().Contain("banana");
        json.Should().Contain("cherry");
    }

    [Fact]
    public void Serialize_WithIntList_ShouldReturnValidJson()
    {
        // Arrange
        var list = new SerializableList<int> { 1, 2, 3, 4, 5 };

        // Act
        var json = list.Serialize();

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Be("[1,2,3,4,5]");
    }

    [Fact]
    public void Deserialize_WithValidJson_ShouldReturnCorrectList()
    {
        // Arrange
        var json = "[\"apple\",\"banana\",\"cherry\"]";

        // Act
        var list = SerializableList<string>.Deserialize(json);

        // Assert
        list.Should().HaveCount(3);
        list.Should().ContainInOrder("apple", "banana", "cherry");
    }

    [Fact]
    public void Save_ShouldWriteJsonToFile()
    {
        // Arrange
        var list = new SerializableList<string> { "test1", "test2", "test3" };

        // Act
        list.Save(_testFilePath);

        // Assert
        File.Exists(_testFilePath).Should().BeTrue();
        var content = File.ReadAllText(_testFilePath);
        content.Should().Contain("test1");
        content.Should().Contain("test2");
    }

    [Fact]
    public void Load_ShouldReadJsonFromFile()
    {
        // Arrange
        var originalList = new SerializableList<string> { "alpha", "beta", "gamma" };
        originalList.Save(_testFilePath);

        // Act
        var loadedList = SerializableList<string>.Load(_testFilePath);

        // Assert
        loadedList.Should().HaveCount(3);
        loadedList.Should().ContainInOrder("alpha", "beta", "gamma");
    }

    [Fact]
    public void SerializeDeserialize_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalList = new SerializableList<int> { 100, 200, 300 };

        // Act
        var json = originalList.Serialize();
        var deserializedList = SerializableList<int>.Deserialize(json);

        // Assert
        deserializedList.Should().Equal(originalList);
    }
}
