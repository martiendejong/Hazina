namespace Hazina.Core.Plugins.Execution;

/// <summary>
/// Exception thrown when a plugin exceeds the timeout limit
/// </summary>
public class PluginTimeoutException : Exception
{
    public PluginTimeoutException(string message) : base(message)
    {
    }

    public PluginTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
