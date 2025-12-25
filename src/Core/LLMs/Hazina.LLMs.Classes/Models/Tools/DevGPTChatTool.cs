using System.Threading.Channels;

public class DevGPTChatTool
{
    public DevGPTChatTool(string name, string description, List<ChatToolParameter> parameters, Func<List<DevGPTChatMessage>, DevGPTChatToolCall, CancellationToken, Task<string>> execute)
    {
        FunctionName = name;
        Description = description;
        Parameters = parameters;
        Execute = execute;
    }
    public string FunctionName {  get; set; }
    public string Description { get; set; }
    public List<ChatToolParameter> Parameters { get; set; }
    public Func<List<DevGPTChatMessage>, DevGPTChatToolCall, CancellationToken, Task<string>> Execute { get; set; }

    public static async Task<string> CallTool(Func<Task<string>> action, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            return $"BigQuery error: {ex.Message}";
        }

    }
}
