namespace Hazina.Core.Plugins.Abstractions;

/// <summary>
/// Result of plugin execution
/// </summary>
public class PluginResult
{
    /// <summary>
    /// Whether the plugin executed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Optional result data returned by the plugin
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Message describing the result (error message if failed)
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Additional metadata about execution
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Create a successful result
    /// </summary>
    public static PluginResult Successful(object? data = null, string? message = null)
    {
        return new PluginResult
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    /// <summary>
    /// Create a failed result
    /// </summary>
    public static PluginResult Failed(string message, Exception? exception = null)
    {
        var result = new PluginResult
        {
            Success = false,
            Message = message
        };

        if (exception != null)
        {
            result.Metadata["Exception"] = exception.GetType().Name;
            result.Metadata["ExceptionMessage"] = exception.Message;
            result.Metadata["StackTrace"] = exception.StackTrace ?? "";
        }

        return result;
    }
}
