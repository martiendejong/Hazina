namespace Hazina.Core.Plugins.Compilation;

/// <summary>
/// Exception thrown when plugin compilation fails
/// </summary>
public class PluginCompilationException : Exception
{
    public PluginCompilationException(string message) : base(message)
    {
    }

    public PluginCompilationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
