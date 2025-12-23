namespace DevGPT.Generator.Tests;

public class DocumentGeneratorBasicTests
{
    [Fact]
    public void DocumentGenerator_ShouldImplementInterface()
    {
        // Arrange & Act
        var implementsInterface = typeof(DocumentGenerator).GetInterfaces()
            .Any(i => i == typeof(IDocumentGenerator));

        // Assert
        Assert.True(implementsInterface);
    }

    [Fact]
    public void IDocumentGenerator_Methods_ShouldReturnLLMResponse()
    {
        // Arrange
        var methods = typeof(IDocumentGenerator).GetMethods();

        // Act
        var getResponseMethods = methods.Where(m => m.Name.Contains("GetResponse") || m.Name.Contains("StreamResponse") || m.Name.Contains("UpdateStore")).ToArray();

        // Assert
        Assert.NotEmpty(getResponseMethods);
        foreach (var method in getResponseMethods)
        {
            Assert.True(method.ReturnType.IsGenericType);
            var genericType = method.ReturnType.GetGenericTypeDefinition();
            Assert.True(genericType.Name.Contains("Task"));
        }
    }
}
