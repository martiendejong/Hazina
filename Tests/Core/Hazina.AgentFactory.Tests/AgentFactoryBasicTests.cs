namespace DevGPT.AgentFactory.Tests;

public class AgentFactoryBasicTests
{
    [Fact]
    public void AgentConfig_ClassExists()
    {
        // Arrange & Act
        var type = typeof(AgentConfig);

        // Assert
        Assert.NotNull(type);
    }

    [Fact]
    public void AgentConfig_ShouldHaveNameProperty()
    {
        // Arrange & Act
        var property = typeof(AgentConfig).GetProperty("Name");

        // Assert
        Assert.NotNull(property);
        Assert.Equal(typeof(string), property.PropertyType);
    }

    [Fact]
    public void ToolsContext_ClassExists()
    {
        // Arrange & Act
        var type = typeof(ToolsContext);

        // Assert
        Assert.NotNull(type);
    }
}
