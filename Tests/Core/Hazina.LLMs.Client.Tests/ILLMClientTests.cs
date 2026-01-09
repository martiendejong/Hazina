namespace Hazina.LLMs.Client.Tests;

public class ILLMClientTests
{
    [Fact]
    public void Interface_ShouldHaveRequiredMethods()
    {
        // Arrange & Act
        var methods = typeof(ILLMClient).GetMethods();

        // Assert
        Assert.Contains(methods, m => m.Name == "GenerateEmbedding");
        Assert.Contains(methods, m => m.Name == "GetImage");
        Assert.Contains(methods, m => m.Name == "GetResponse");
        Assert.Contains(methods, m => m.Name == "GetResponseStream");
    }

    [Fact]
    public void Interface_Methods_ShouldReturnLLMResponse()
    {
        // Arrange
        var getImageMethod = typeof(ILLMClient).GetMethod("GetImage");
        var getResponseMethods = typeof(ILLMClient).GetMethods()
            .Where(m => m.Name == "GetResponse" && !m.IsGenericMethod)
            .ToArray();

        // Assert
        Assert.NotNull(getImageMethod);
        Assert.True(getImageMethod.ReturnType.IsGenericType);

        // GetImage returns Task<LLMResponse<T>>, check the Task's generic argument
        var taskReturnType = getImageMethod.ReturnType.GetGenericArguments().FirstOrDefault();
        Assert.NotNull(taskReturnType);
        Assert.True(taskReturnType.IsGenericType);
        Assert.Contains("LLMResponse", taskReturnType.Name);

        Assert.NotEmpty(getResponseMethods);
        foreach (var method in getResponseMethods)
        {
            Assert.True(method.ReturnType.IsGenericType);
        }
    }
}
